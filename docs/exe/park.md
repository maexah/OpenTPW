# The park: data layout and engine behaviour

What a Theme Park World park is made of on disk, and what the original executable does with it. The on-disk material was measured against the real game files, not inferred from the exe; the executable material was read from the disassembly, and in several places adversarially re-verified. Everything here is about `jungle` (Lost Kingdom) unless a theme is named: the four parks are re-skins of one another, so whatever is built for jungle is the engine for all four and only the data changes.

**Part 1 is file-format material and is duplicated in the FileFormats docs clone** (most of it is already there). It is kept here because Part 2 constantly refers to it. Part 2 — executable behaviour — exists only here.

Confidence markers are part of the facts. Where the source says *inferred*, *not proven*, *unknown* or *refuted*, that word is load-bearing and has been carried across verbatim.

---

# Part 1 — On-disk formats (duplicated in the FileFormats docs)

## A park is a folder under `data/levels/`

`jungle` = **Lost Kingdom** (island 0), `fantasy` = Wonder Land, `hallow` = Halloween World, `space` = Space Zone.

`data/levels/jungle/` holds: `terrain.wad`, `sharetex.wad`, `ssharete.wad`, `miscmesh.wad`, `queue.wad`, `hoarding.wad`, `advisor.wad`, `dynamic.wad`, `2dmap.tga` (768K minimap), `scape.omp`, `test.tpt`, `Easymode.TPWI`, and the `.sam` settings files.

The terrain `.MAP` files live **inside `terrain.wad`**. A filesystem search for `*.map` finds only sound-category maps (`cat_*BANK.map` / `cat_*SFX.map`, already implemented) plus `SndReverb.map`; there is no loose terrain `.MAP` anywhere. **Do not conclude from a `find` on the game folder that the format is unused.**

## `terrain.wad` — DWFB archive, 205 entries, the whole park's land

| Entry | Uncompressed | What |
|---|---|---|
| `base.MD2` | 1,377,696 | the park's fixed scenery mesh, plus the heightfield block the ground is built from (`park-engine.md`, "The heightfield lives inside base.MD2") |
| `basem.MD2` | 9,416 | second, much smaller mesh — the animation half |
| `base.lnd` | 2,417,116 | procedural-texture source data, optional — not the landscape (see `.LND` below) |
| `base.map` | 16,464 | 16384 + 80 -> 128x128 grid + 80-byte header (hypothesis; the real header is 72 bytes, see `.MAP` below) |
| `terrain.map` | 16,464 | same size, same shape |
| `Jungle.tct` | 615 | per-theme texture table |
| `qickload.txt` x5 | - | texture manifests, one per texture dir |
| 194 x `.wct` | - | in `pathtex/`, `spathtex/`, `stexture/`, `textures/` |

Path-tile art is named by topology: `jpa_cnr1` corner, `jpa_ctr1` centre, `jpa_edg1/2` edge, `jpa_end1` end, `jpa_squ1` square, `jpa_str1/2` straight, `jpa_tju1..4` T-junction, `jpa_xrd1/2` crossroads.

## WAD container layout

Confirmed from `WadArchive.cs` and verified by hand against the header: 4 magic `DWFB`, 4 version, 64 padding, 4 file count, 4 list offset, 4 list length, 4 null = **88-byte header**; then **40-byte entries** (4 unused, 4 name offset, 4 name length, 4 data offset, 4 data length, 4 compression (4 = refpack), 4 uncompressed size, 12 null). A name containing `\` sets the current subdirectory for every entry after it.

## `.MAP` — the TP2M attribute map

`base.map` / `terrain.map` are self-describing. Layout confirmed against all four parks:

    0x00  "TP2M"
    0x04  "Theme Park 2 Attribute Map File\0"   (32 bytes)
    0x24  "MAP "                                 chunk tag
    0x28  uint32 chunk length = 16412
    0x2C  uint32 width  = 128
    0x30  uint32 height = 128
    0x34  uint32 x5     = 8, 8, 8, 8, 8         (20 bytes, meaning unknown)
    0x48  128 x 128 attribute bytes              <- body starts at 72, NOT 80
          (file is 16464; 44 + 16412 = 16456, leaving 8 trailing bytes unexplained)

**Indexing is `index = x * 128 + y`** — X is the major axis. Proven, not assumed: all ten `FixedItemInfo` positions from `Standard.sam` land on a meaningful attribute under `x*128+y` and on 0 under `y*128+x`. TicketBooth (47,13)/(48,13) -> 148; BusStop (42,5)/(53,5), both Crossings and StrikeAreaStart (40,9) -> 144; Entrance (47,17)/(48,17) and FixedItemOrigin -> 8.

Attribute values seen: 0 (empty), 1, 3, 8 (park entrance), 17 (0x11, the bus/road approach, 490 cells), 128, 144 (0x90, road/bus/crossing, 48 cells), 148 (0x94, ticket booth, 14 cells). They look like a bitfield. **Values 17, 144, 148 and 8 occupy byte-identical positions in all four parks** — the fixed infrastructure every park inherits. Only the 0/1/3 cells differ per park.

The attribute map's non-zero region is exactly x 0..95, y 0..84 — reproducing `Standard.sam`'s 95x84 from a different file entirely. Value 17 (the bus road) occupies x 28..67, y 0..16; value 3 occupies x 29..57, y 39..64.

**`base.map` carries NO player-laid paths — do not look for them there.** Jungle's `base.map` has just **66** cells with 0x80 set — the bus road (144), the two crossings and the ticket booths (148), plus four bare 128s — and **every one of the eleven object cells reads attribute 0 with no 0x80 neighbour**. The attribute map describes the fixed approach every park inherits, not what a player built.

## `.TCT` — the texture index table, plain text

`<Theme>.tct` ("Texture Correspondance Table") is ASCII with CRLF: comment lines starting `#`, then a section name on its own line, then `index<TAB>name.tga` rows. Jungle has `PathTex` 0..21 and `QueueTex` 0..3, plus a commented-out `#Water` section. **The names end `.tga` but the files on disk are `.wct`** — the extension must be swapped when resolving.

Full `PathTex` table: 0 squ1, 1 end1, 2 str1, 3 cnr2, 4 tju1, 5 xrd1, 6 tju2, 7 tju3, 8 xrd2, 9 cnr1, 10 edg1, 11 icn1, 12 icn2, 13 ctr1, **14 ctr1 again** — index 14 is deliberately a duplicate of 13, carrying the original author's own comment *"14 has be put back as a dummy texture - maybe fix later ?"*, **so do not treat it as a parsing bug** — 15 tju4, 16 baseblue, 17 grnarrow, 18 redarrow, 19 str2, 20 edg2, 21 dark. `QueueTex` is only 0..3 (`jpa_que4, que3, que1, que2`) — note the names are **not** in numeric order.

## `base.MD2` — an ordinary static mesh, and the park's fixed scenery

Magic `0x1CD15D46` with the 0xDD/0xCB constants at 0x04/0x08 — exactly what `ModelFile` already reads, and the same header the lobby's own models carry. **The terrain mesh needs no new model code.**

Parsed with the project's own `ModelFile`, unmodified:

    Meshes 272, Nodes 274, 30,124 vertices, 61,215 indices = 20,405 triangles
    World bounds X -554.8..1511.0  Y -11.0..99.1  Z -621.0..1444.8
    Footprint 2065.8 x 2065.8 (exactly square), height range 110.1

The transformed vertices reproduce the header bbox at 0x80 exactly, which cross-checks the parse.

Mesh names are the park itself: `road_center`, `road_lhs`, `road_rhs`, `arrival_base/roof/roof_support`, `depart_*`, `ticket_booths`, `front_hoarding`, `right_hoarding`, `verge01..05`, `basic hoard*` (92), `palm*` (18), `river*` (9), `surface*` (9), `newcliff*` (11), `volcanoe`, `flag_pole*`, `Box*`, `Object*`. These are the same fixed items `Standard.sam` places by grid coordinate.

`Mesh.Materials` (`MaterialData`) already carries `Name` (e.g. `grd_ctr1`), `Flags`, `IsTranslucent`, `StartIndex`/`EndIndex`, so material-to-texture needs no new code. **All 61 textures `base.MD2` names resolve** to `.wct` files in the park's own archives (terrain/sharetex/ssharete/miscmesh/dynamic), and **57/57 distinct material names open as `levels/jungle/terrain/textures/<name>.wct`** through the real `BaseFileSystem` — exactly the string `LobbyModel` builds.

`basem.MD2` reports `IsAnimation: True` with 0 meshes, exactly as `ModelFile` documents. It is `readable True` with **11 UV tracks over frames 0..100** — UV scroll is how this game moves water, as `Jun_isleM1` laps the lobby island's shoreline; the river and the falls are the likely candidates. **Only jungle ships a `basem.MD2`**; fantasy, hallow and space ship `base.MD2` alone.

**A first park render needs no new file format**: `base.MD2` through the existing `ModelFile`, its textures through the existing WCT/WAD path, and a camera. `.MAP`/`.TCT` are needed for gameplay (attributes, editing, path tiles), not for the first picture; `base.lnd` is not needed at all.

## `.LND` — procedural-texture source, not the landscape

Header: byte 0 = 3 (version?), 10 bytes of per-park values, then uint32 384, uint32 344, uint32 4 at offsets 11/15/19 — **the same 384/344/4 in all four parks** — then 4-byte quads. There is a section of 384 x 344 x 4 bytes = 528,384 somewhere in the middle, and ~1.8 MB beyond it. Sizes are park-specific (jungle 2,417,116; fantasy 2,196,802; hallow 2,447,361; space 2,241,690). The heights are not here: they are a block inside `base.MD2`, read by `HeightfieldFile` — see `park-engine.md`, "The heightfield lives inside base.MD2". The engine loads `base.lnd` only when `DAT_007a1a8c & 0x2000` is set (`park-engine.md`, "base.lnd is not the heightfield").

## `Standard.sam` — the park's specification, and the simulation's balance file

`data/levels/jungle/Standard.sam`, 144 lines, read by the existing `SettingsFile`. Plain text. It means the map dimensions, lighting and fixed-item placement do **not** need reverse engineering.

    MapInfo.HeightfieldXStart 0   YStart 0   Width 95   Height 84   <- "Heightfield offset from map mesh"
    MapInfo.FixedItemOriginX 48   FixedItemOriginY 17
    FixedItemInfo.{TicketBoothA/B, BusStopA/B, CrossingBSSideA/B, CrossingParkSideA/B, EntranceA/B}Pos{X,Y}
    FixedItemInfo.StrikeAreaStartX 40 StartY 9 SizeX 6 SizeY 1
    ThemeEngine.FogColour 4774136   AmbientLightLevel 4283782504   DirectionalLightLevel 4294967256
    ThemeEngine.ProceduralTexturingXOffset -1   YOffset -1
    LightNormal.X 0.4   Y -0.8   Z 0.4          <- the park's sun, MEASURED not guessed
    WaterStartPos.X 380  Y -5   Z -180.0
    ThemeAdvisorCostumes[0].AddOns 4 / SubOffs 19, [1].SubOffs 20
    Seasons[0..3], Weather.*, WeatherEffects.* (rain/lightning thresholds, thunder distance)
    GoldenTicketLocal.*, ChallengesInThisLevel[0..7], LoanInfo[]

**95x84 playable heightfield inside a 128x128 map mesh** is the reading that makes the sizes agree. `Easy_Standard.sam` has **no** `MapInfo` — it is an override layer over `Standard.sam`, not a replacement. `global.sam` is the small file the **lobby** reads before a theme loads (ticket rules, `ParkName.GateObjectId 1601`).

**The base `data/levels/Standard.sam` is the simulation's balance file**, 588 lines against jungle's 144, and it is the layer the theme file overlays. Its groups: `PeepInfo` (**33 keys**), `PeepTypes[0..7]` (8 rows of `PreferredExcitement` / `StartingCash` / `BoredomThreshold`), `StaffPoolInfo`, `Arrival`, `AllStaffConstants`, `PerGradeStaffConsts[0..4]`, `PerTypeStaffConsts[0..4]`, the four `*ConstsPerGrade`, `BankAccountInfo`, `LoanInfo[0..7]`, `Research`/`ResearchTech`/`ResearchCategories`, `Costs`, `Challenges`, `GoldenTicketGlobal`, plus the `MapInfo`/`FixedItemInfo`/`ThemeEngine`/`Seasons`/`Weather` groups a theme overrides.

**The peep simulation's constants live here, not in the exe.** The block at `0x00785000` that the exe's peep code reads is **all zeros statically** — it is filled by the `BalanceLoader.cpp` parser from this file at load. So the tunables were never something to reverse engineer, and `ParkBalance` (which layers base + theme) can read them: `Balance.Int( "PeepInfo.ExitLevel" )` works. The keys are self-documenting — the file carries trailing comments like *"toilet level above which peep is 'desperate'"*. **Themes barely touch it:** only `PeepInfo.ExcitementToCostDivisor` is overridden anywhere (fantasy and space, 4 -> 5); jungle and hallow override no peep key at all.

### The parks really are re-skins

All four `Standard.sam` files declare **identical** `MapInfo` (heightfield 95x84 at 0,0; FixedItemOrigin 48,17), identical `LightNormal` (0.4, -0.8, 0.4), identical `WaterStartPos` (380, -5, -180), identical `AmbientLightLevel` and `DirectionalLightLevel`. **Only `ThemeEngine.FogColour` differs.** One park engine serves all four — build it once, properly.

### The ThemeEngine colours are 0xAARRGGBB

    FogColour  jungle/fantasy 4774136    = 0x0048D8F8 -> R72  G216 B248 (pale blue; cf lobby 0x44DDFF)
               hallow         1122884    = 0x00112244 -> R17  G34  B68  (storm dark)
               space          16286768   = 0x00F88430 -> R248 G132 B48  (orange)
    AmbientLightLevel         4283782504 = 0xFF555568 -> R85  G85  B104
    DirectionalLightLevel     4294967256 = 0xFFFFFFD8 -> R255 G255 B216

Alpha is 0 on the fog values and 255 on the light values; every channel reads sensibly, which is the evidence for the byte order. `hallow` also ships a commented-out alternate ambient/directional/LightNormal set — **do not "restore" it.**

## `Easymode.TPWI` — the Instant Action park

Container: the preamble laid out in `park-engine.md`, "The save container" (a u32 version, 400 here and 500 in a player save; a pad byte; 824 bytes of UTF-16LE copyright notice; the file-info and `BILZ` headers), then **a single zlib stream at file offset 0x629** which inflates 38,479 -> **1,608,309 bytes** with no trailing data. Only `jungle` ships one, which is why Lost Kingdom is the Instant Action park. Same container family as `.TPWS` (player park saves). OpenTPW inflates the `.TPWI` with `SaveReader` and walks its payload with `ParkWorld`; nothing in OpenTPW opens a `.TPWS` yet.

The inflated payload names the park's features by path — `data\levels\jungle\Features\gates\`, `\Features\bus\`, `\Features\toilet\`, `\Shops\coconut\`, `\Rides\bouncy\` and more — so the gate is a save-placed object, not part of the terrain model.

## Formats that are not the problem

`test.tpt` (jungle + space only, 236 bytes) is a weather table: dword chunk count 6, then per chunk a 4-byte tag, 4-byte size, payload — `WTHR`, `TEMP`, `WNDS` (wind speed), `WNDD` (wind direction), `CLCV` (cloud cover), `ADSR`; each carries a count then five (value, weight) pairs. Decodable by inspection; named "test", probably vestigial.

`scape.omp` (409-590 bytes, one per park) is an `OBJ_` record file ending in an `INCL` chunk (count, then length-prefixed strings) naming the C++ headers it was built from: `D:\Park2\data\Particle\par_lib.h`, `SfxEvent.h`, `JungleEvent.h` — so it binds particle and sound events to the theme. Small; low priority.

## Buildable items: the per-item archive

An item is a `.wad` under `features/`, `shops/`, `rides/` or `sideshow/`, standing in for a directory of its own name, and everything inside is named after that stem: `<stem>.sam` (its description), `<stem>.MD2` (its model), `<stem>.hmp` (its footprint), `<stem>.sgn` (a name board — rides only), plus `textures/` and `stexture/`. Jungle holds **67** items across those four folders.

**An item ships only the art unique to it.** Everything else comes from the theme's shared archives — `sharetex.wad` (full size, pairs with `textures/`) and `ssharete.wad` (low detail, pairs with `stexture/`), 116 members each, and **all four themes ship both**. Without that fallback the eleven objects Lost Kingdom places were missing **40 distinct textures** and drew the not-found art on most of their surfaces. Every missing name was present in both archives.

**An item's footprint is the BOX its `Info.Shape` picture is drawn in, not the cells marked inside it.** `4x4rock` draws 14 stars in a 4x4 box, `5x5rck` 23 in 5x5, and `ground`/`groundc`/`mystery` draw none at all — yet every one of the 70 jungle items' `.hmp` length says `48 + 27 * (width * height)`. **`gates` is the only jungle item carrying `Engine*Override` keys**, and it needs them: its picture is a single cell where its real footprint is the 6x3 its own `.hmp` declares.

The model is authored with its **footprint's corner at its own origin** — a 1x1 toilet's floor spans 0..10, the 3x3 fountain's 0..30, the 2x2 staff room's 0..20 — read from node `WorldTransform`, never from bounds, which are node-local and say only how big a mesh is.

**Every item's model opens with a flat floor plate as wide as its whole footprint** — `J_WC`, `wf_floor`, `js_base`, `cn_floor01`, `jb_floor`, `jc_base` — and all 44 footprint cells carry a real ground index, so the ground must skip them or the two fight for the same depth.

## `.sgn` — one layout, walked in order

`SignFile` reads all 84 signs by walking the header in the engine's own order (`FUN_005ec3a0`): 61 leave the
artwork byte at `0x08` clear, and three (jelly, zob, C_SCAT) omit both ink blocks, which is what made the header
look like it had two fixed sizes. The layout is in the FileFormats clone, `formats/sgn.md`, on its
`docs/sign-format-corrections` branch (not yet on master). The `.sgn` is always named after the
item's own folder — **78 of 78 across all four themes**.

## `.RSE` — the ride-script container

Magic and version are uniform across the whole shipped corpus, **checked not sampled**: 312 wads scanned, 308 scripts extracted, and **308 of 308** carry magic `RSSE` and version `0x00010F51`, none malformed. Sizes run 88 to 2,172 bytes, mean 577. All 308 parse to their last byte and walk instruction by instruction to their last word.

    0x00  char[4]   "RSSE" (0x45535352 LE)
    0x04  int32     version 0x00010F51, compared against DAT_00879190; a mismatch only WARNS
    0x08  int32     variable count            -> loader field +0x23, allocates count*4 ints
    0x0c  int32     count                     -> +0x15   (the STACK SIZE)
    0x10  int32     50 in every shipped file  -> +0x25   (the TIME SLICE, an instruction budget)
    0x14  int32     LIMBO slots, 8 bytes each -> +0x16, array at +0x24
    0x18  int32     BOUNCE slots, 16 b. each  -> +0x19, array at +0x28
    0x1c  int32     WALKON slots, 32 b. each  -> +0x1f, array at +0x2c
    0x20  byte[16]  four dwords the loader reads and DISCARDS - always "Pad Pad Pad Pad "
    0x30  int32     script length in DWORDS   -> +0x14
    0x34  int32[]   the script body           -> +6
          int32     string blob length        -> +0x24
          byte[]    the blob: NUL-terminated strings, referred to by BYTE OFFSET
          ...       one length-prefixed name per declared variable (length includes the NUL)

**The variable names at the end are the trap.** The loader allocates the variables' storage from the count and never reads their names, so a reading that stops at the string blob leaves a tail it cannot explain. Off that reading **98 of 308 looked well-formed and 210 looked broken — and the 98 were exactly those declaring no variables.** Parsing the names accounts for the last byte of all 308. **74 distinct names corpus-wide**, every one `VAR_*`; the twelve `RideVariables` lists are the first twelve of 129 scripts, in that order, which is why `VAR_RIDECLOSED` is index 6.

**Where the scripts live:** none are loose on disk — every one is inside `data/levels/<theme>/{rides,shops,sideshow,features,upgrades}/*.wad`. `EventMap.RSE` appears **28 times** out of 308, so it is a minority of archives, not most. Four names recur across themes (`wateride`, `gokarts`, `gates`, 4 each); the rest are unique. Extensions are **305 `.RSE` to 3 `.rse`** and case is inconsistent in the data (`Coaster1.RSE`, `Monkey.rse`, `child.RSE`), so **any lookup must be case-insensitive** — mandatory here in a way it never was on the original's file system.

---

# Part 2 — Executable behaviour

## The grid: cell = 10, `world = grid * 10 + 5`

    world_centre_of_cell(g) = g * 10 + 5        (cell size 10, grid origin at world 0)

**Two wrong guesses were made and discarded before this, recorded so they are not made again:**

- `world = meshMin + grid * (span/128)` (= 16.139/cell). **Wrong.** It put `ticket_booths` at grid (64,47) instead of (47,13). The tidy "2065.8/128 = 16.139" was a coincidence of dividing by 128: `base.MD2` spans the whole scenic island (sea, cliffs, surround), so **the mesh extent is not the playable grid extent.**
- `cell = 100/11 = 9.09`, inferred from the two bus shelters. **Wrong** — it fits X and fails Z, and a vertex-quantisation scan scores it at 5.4%, i.e. noise.

**What the geometry votes for.** Scanning all 30,124 world-space vertices for the cell size that puts the most of them on a gridline: **5.0 -> 41.1% X / 39.6% Z** and **10.0 -> 37.5% / 36.0%**, against 9.09 at 5.4% and 16.139 at 2.2%. So 5 is the modelling sub-grid and **10 is the game cell**.

**Evidence.** `ticket_booths` is the only mesh whose grid coordinate comes straight from `Standard.sam` (`TicketBoothA/B PosX 47/48, PosY 13`) rather than from a guess: predicted (480.0, 135.0), actual (480.0, 135.0), **error 0.0 on both axes**. The other named meshes carry guessed grid coordinates, and their residuals are structured rather than random — both bus shelters sit exactly one cell before their stop in Z and are inset symmetrically in X — which is a mesh-centre offset, not a broken transform. **Treat the cell size as proven and per-item offsets as unknown.**

Playable 95x84 -> world 0..950 x 0..840. Map 128x128 -> 0..1280. `base.MD2` spans -555..1511 x -621..1445, so the park sits inside a much larger scenic island.

**Two indexing conventions, opposite to each other, both proven:**

| Data | Index | Proof |
|---|---|---|
| `.MAP` attribute bytes | `x * 128 + y` | all ten `FixedItemInfo` positions land on a meaningful attribute; zero under the other order |
| save world-block cells, and the heightfield | `y * 128 + x` | drawing both: y-major reproduces `base.map`'s bus road, ticket booths and entrance column exactly, x-major gives incoherent blobs; `FUN_005365d0` derives `x = (id-1) & 0x7f`, `y = (id-1) >> 7` |

## What covers a cell: ground index 0

**Index 0 = something covers this cell.** That something is either geometry already in `base.MD2` (the river's water and banks, the cliffs) **or a placed FEATURE loaded from the park save.**

Measured by cross-referencing `base.MD2`'s own per-cell texture index against the save's `mType`:

    all 82 path + queue cells      carry a REAL ground index (27 jgr_bas1 x56, 57 x16, 58, 59, 60) - NONE is 0
    of the 1,159 index-0 cells     ZERO are path or queue
    mType 30, the fixed approach   66 of 66 ARE index 0    <- the bus road, drawn by road_center/lhs/rhs

So **a player-laid path cell is ordinary drawn ground**. Index 0 means "something covers this", but that something is the river, the cliffs and the **fixed** road furniture, **never a built path**.

**Consequences for a path renderer, and this is the whole design:** a path layer laid *over* the ground would z-fight, because the ground quad is really there. And the ground's material cannot simply absorb the path art either — it holds **16 texture slots** and the jungle already uses 6 for its ground bases, while the paths want 11 distinct `PathTex` rows and the queues 3 more. The shape that works is the ground skipping the cells the save gives a tile to, and a separate entity drawing those cells with its own material — no overlap, no slot overflow. The heightfield is 96x85 at 10x10 units, the same grid the save's cells use, so the two line up cell for cell with no conversion.

### The two pale-blue quads flanking the jungle's gate are not a ground bug

They are holes with the fog clear colour showing through, and they appeared the moment index-0 cells stopped being drawn. They sit **exactly where the park's entrance gate stands** (identified from screenshots of the original). `base.MD2` carries only **`undergates`**, a 14-vertex strip of ground laid *under* the gateway at x 450..510, z 180..190, textured `jgr_bas1`/`jgr_bas6`; it covers the arch and not the cells either side of it.

So the rule "skipping index-0 cells uncovers the park rather than holing it" is right about the river and the roads, which have scenery under them, and **overstated as a universal**: some index-0 cells have nothing beneath. The holes are where the park's gate stands; `ParkFixedItems` now stands the gate there, and whether its model covers them has not been recorded. **Do not "fix" this by drawing index 0 again** — that is what paved the river in road tarmac.

## Item animation: the twelve roles

The engine's loader `FUN_00461f10` probes for an item's clips with a **12-entry suffix table**, in lockstep with twelve slots on the model record.

| Address / offset | Original name | What it is | Evidence |
|---|---|---|---|
| `FUN_00461f10` | - | item loader; composes `"%s%s%c.md2"` and `"%s%s%c%d.md2"` — directory, stem, letter, optional number. Also the source of a model's channel count (its allocation argument) | read |
| `0x006fe6bc` | - | the twelve-entry letter table, **stride 8** | `MOV [ESP+0x14],0x6fe6bc` at `0x004622fb`, `MOV [ESP+0x20],0xc` at `0x00462303` |
| `0x74d2b4` | - | format string `"%s%s%c%d.md2"` | read |
| `0x74d2a8` | - | format string `"%s%s%c.md2"` | read |
| `FUN_004629d0` | - | loads a whole second model+animation set as `"p%s"`. A leading `P` is a **prefix**, not a suffix; **what it is for is not known** | read |
| `FUN_00463060` | - | build path: checks role 0 exists, plays it, then queues `0xd` (not a clip — it binds nothing and only re-stamps the channel's timers) | read |
| `FUN_004647a0` | - | save load: overwrites every animation channel with the saved state and restores the per-node flag words with it | read |

    id 0  C      id 3  L      id 6  E      id 9   B
    id 1  D      id 4  S      id 7  U      id 10  R
    id 2  I      id 5  M      id 8  W      id 11  O

Probe rule: `<prefix><stem><suffix><n>.md2` with **n from 1 upward** until a probe fails, and **if no numbered file exists** it falls back to the unnumbered `<stem><suffix>.md2`. So `bouncyb/c/i/r` are four roles with one clip each and `JunsprayM1..M6` is one role with six.

**The exe assigns the letters no meaning** — only ordinals — so "C = construct" is safe (the build path hard-codes role 0) and the rest are inference. Only the letter is ever read (`(int)*pcStack_42c`). The second dword of each table entry runs 1..11 and then a float, so **it is not established as part of the table and nothing should lean on it**.

**Cross-validated without the binary**, against the ids used by the 72 ride scripts whose own wad ships each letter: `c` -> id 0 at **1.00 against a 0.00 base rate**; `i` -> id 2; `l` -> id 3 (0.91 vs 0.13); `s` -> id 4 (0.94 vs 0.03); `e` -> id 6 (0.93 vs 0.02); `b`/`r` -> ids 9 and 10, which co-vary because those two files travel together. Six letters land exactly where the table puts them, from data that never touched the executable. `m` shows no signal only because id 5 is used by 55 of the 72 wads, so it cannot discriminate.

**REFUTED: an id is not an index into the `M<n>` clips.** Of the 72 ride wads whose script names a literal id, **all 72** have a highest id at or above the number of `M<n>` files they ship: `gokarts` ships **none at all** and triggers ids 0 and 5, `jelly` ships none and reaches 10. `M` is simply the letter of **one** role — id 5 — the one whose files carry numbers.

**Role 0 is played once, when the player builds the thing.** A park loaded from a save does not replay it, so **the last frame of the construction clip is what a built item looks like**.

Two independent reads of the role-slot layout on the model record, both recorded as read:

| Offset | What it is | Evidence |
|---|---|---|
| `model+0x18 + role*0xc` | the twelve role slots | channel decode pass |
| `model+0x1c + id*0xc` | twelve 12-byte slots `{count, pointer-to-entries, ...}`, entries 8 bytes; entries at `model + (id+2)*0xc + 8` | the trigger `FUN_004732a0`, the reset `FUN_00473e30` (a `do {...} while (i < 0xc)` walk at stride 3 dwords) and the predicate `FUN_00472d10` (`id != 0xc && flags & 3 && frame < count`) agree independently |
| `model+0x14` | count of channels with role < 12 | read |
| `model+0xa8` | total clips loaded across the twelve roles | read |

Each role entry is 8 bytes, `{source index, animation pointer}`, and the pointer is the block at **file offset 0x98**.

**The sentinel for "no role" is 12**, so the id space is 0-11. Roles **13 and 14** are pseudo-roles: `0xd` sets channel flag `0x2` (freeze at frame 0), `0xe` sets `0x14` (hold the last frame), and end-of-clip on a non-looping channel re-enters with `0xe`.

**`FUN_0044b220(model, 0x80, n)` at the loader's `+0x30`/`+0x4c` is NOT animation.** It is the guest-attachment system — `ADDHEAD` (56) and `DELHEAD` (57) are its only readers. Do not confuse that array with the twelve role slots.

### Clip length is the span the clip declares, not what its keys cover

The trigger reads the block's `+0x04` and `+0x08` as **integers** — read as floats they give denormal nonsense — and multiplies the difference by the float **33.3333 at `0x006fec08`**, which is exactly 1000/30.

**Measured over the 1,140 clips under `levels/` that carry a block: every one declares a start of nought and an end of at least one, and 159 disagree with what their keys span** — 129 short (114 of them reading as no span at all through our own reader, which rejects clips carrying only positions and visibility) and 30 long, where the keys run past the declared end and the engine never plays them.

**`FUN_00474070` is NOT this function**: it multiplies `clip[+8]` alone with no subtraction, and its one caller is in the advisor's range. **Do not cite it for ride timing.**

## The animation channel — `AnimTimeControl`

The struct has a shipped name: `FUN_00464580` is a debug dumper that prints every field by name, and it indexes as `(channel - model[+0x10]) / 0x38`, so the exe's own arithmetic confirms the stride. **Use these names.**

| Offset | Original name | What it is |
|---|---|---|
| `model+0x0e` | - | ushort channel count |
| `model+0x10` | - | channel array, **stride 0x38** |
| `channel+0x00` | `Flags` | flag `0x1` = loop (`LOOPANIM` at `0x00552bd1` passes flags 1; `TRIGANIM`/`WAITANIM` pass 0, all on channel 0); `0x2` = freeze at frame 0; `0x14` = hold last frame; `0x40` selects the alternate frame snapshot |
| `channel+0x04` | `AnimID` | role (12 = none) |
| `channel+0x08` | `SubAnim` | entry |
| `channel+0x0c` | - | float speed (1.0f) |
| `channel+0x10` | `StartAnimTime` | |
| `channel+0x14` | `AnimTime` | |
| `channel+0x18` | `NoPauseAnimTime` | **read at `0x004646a1`, not dead** |
| `channel+0x1c` | `TotalAnimFrames` | `clip[+8] - clip[+4]` |
| `channel+0x20` | `AnimFrame` | elapsed frames |
| `channel+0x24` | `DeferredAnimID` | queued role |
| `channel+0x28` | `DeferredSubAnim` | queued entry |
| `channel+0x2c` | `DeferredFlags` | queued flags |
| `channel+0x30` | - | queued speed |
| `channel+0x34` | - | the clip last posed — **read by `FUN_004726d0`, not write-only** |

`elapsed = (now - start) * speed * 0.03` (`_DAT_006fec0c`), the subtraction **unsigned**; finished is `total < elapsed`, **strictly** — equality still plays.

`FUN_004732a0(model, role, entry, flags, speed, channel)` starts at once only if the channel is idle, finished, flagged `0x6`, the role is 13/14, or flags carry `0x2`; **otherwise it QUEUES** and answers the remaining time plus the new clip's length (or plus a flat 1000). The two terms are **truncated separately**. Queue clear resets `+0x24/+0x28/+0x30` and deliberately **leaves `+0x2c` stale**. **`flags & 0x2` means "start now AND clear the queue"** — that block is reached only on that path, and it writes `+0x24 = 0xc`, `+0x28 = 0`, `+0x30 = 0`.

**The "+1000" fallback is not a simple if-then.** Reaching `ADD ESI,0x3e8` at `0x004733e7` needs **eight** earlier gates to pass: the currently-playing entry must itself name a real clip, its timing must satisfy `[+0x20] <= [+0x1c]`, `[ESI] & 6` must be clear, `param_2` must be neither 13 nor 14, and `param_4 & 2` must be clear. **In the ordinary idle case — nothing playing, which is every fresh script — `0x004732ef` diverts to `LAB_0047340b` instead**, and the answer is `FUN_00472f60`'s, or a **flat** 1000 with no remaining-time term added. So "role absent gives playing-time + 1000" is true only mid-animation.

**A trigger naming a role or entry the model lacks does more than answer 1000**: `FUN_00472f60` calls the rest-pose restore `FUN_00472310` and parks the channel at role 12. Eight shipped references do this.

### Who advances a channel, and when

`FUN_004735d0` advances one channel, called once per channel from `FUN_00473c70` (stride 0x38, correct), called from the per-frame sweep `FUN_0044e410` at `0x0054fa96` — **after the 31 ms catch-up loop's back edge at `0x0054f8da`**. It takes **no time argument**: it reads `DAT_007b496c` (or `DAT_007b4974` when channel flag `0x40` is set), a snapshot written once per frame by `FUN_00473440` at `0x0054f475` from the clock object at `0x785970`.

**Animation advances once per frame off one snapshot while scripts tick at 31 ms** — several sim ticks in one frame still produce exactly one advance. Confirmed by call graph: `FUN_004735d0` has **exactly one** caller, `FUN_00473c70` at `0x00473d2e`, and **not one** of that function's ten call sites is the script system `FUN_005516b0` or sits inside the 31 ms loop; the park's are `FUN_0044e410(2)`, `FUN_00429df0(0)` and `FUN_00429df0(1)`.

**Two candidates were checked and cleared, and either would have inverted this:** `FUN_00473440` runs once per frame *above* the loop, so it looks like the sweep, but only computes the frame delta into `DAT_007b497c`; and `FUN_00475360` does run inside the loop every 2nd tick, but is a periodic-task scheduler (`+0x7c` due time, `+0x80` interval) that calls none of this.

The consequence for OpenTPW: the advance belongs in the frame sweep, **not in the tick**. With the advance in the tick, end-of-frame state was identical either way, but a clip ending mid-catch-up promoted its queued successor early and the next tick's instructions could see it. **Do not put it back in the tick.**

**Two gates, and they invert the idle default.** `FUN_00473c70` touches nothing unless `model+0xa8` is non-zero, and the sweep will not call it unless `model+0x14` is non-zero. `FUN_00473e30` parks every channel at role 12 on load and only a START raises `+0x14`, so **nothing animates until something triggers it and the role-5 idle default cannot fire on a freshly loaded park.** Once a clip has finished the default fires every frame channel 0 reads finished, three attempts at most, with flags 8 — no rest restore, no hide list.

**The idle default is role 5 entry 0 on channel 0**, restarted when channel 0 is idle or finished, gated on `(model+4 & 0x8004) == 0`. Entry 0 is a literal — **nothing in the engine ever advances to a role's next entry**, so cycling every `M` clip has no engine counterpart. `thing+0x14` gates whether a thing is ticked at all: it counts channels with role < 12, and a stopped model drops to nought and is never restarted — so an unconditional idle restart would start role 5 on every static prop in the park.

### End of clip is five outcomes, not three

Gated on `model[+4] & 0x18`: promote the queue; else if `0x18` clear, replay when flag `0x1` else pseudo-role `0xe`; else if `0x8` clear, **stall** (pin elapsed to total, set `model[+4] |= 0x40400000`, defer a stop to the end of the frame); else **stop and call out** to the dispose/reposition group. The replay's speed comes from `+0x30` when non-zero, only falling back to `+0xc`.

**"Freeze" is not "stop posing".** `FUN_00472f60`'s `0xe` pins `elapsed := total`, so the clip is posed **once more at its true final frame** and held; `FUN_00471860`'s early return never fires for it. A rest-restore does exist (`FUN_00472310`, master -> instance, differential at a clip switch) and the freeze paths suppress it by passing bit `4`. **In blend mode (`model+0x30 & 4`) the rest pose is re-laid every frame and a finished channel snaps back** — the opposite of freezing.

**Continuity is a move of the start stamp, not an accumulator.** `FUN_00472bc0`: total is `(float)(uint)(clip[+8] - clip[+4])`; a reset stamps `+0x10`, `+0x14` and `+0x18` from one snapshot; an overrun instead sets `+0x10` and `+0x18` to `+0x14 - trunc(carry * 33.333.. / speed)` with the carry clamped to the new clip's own total. **A float delta accumulator cannot express this.**

### Channels are per-model, unbounded, and one function is buggy

Channels are per-model animation players, count at `model+0x0e`, array at `+0x10`, stride `0x38`, and **none of the three functions that index them bounds-checks against that count**. A model is built with as many as its creator asked for — one for scenery (eight call sites), five for a coaster's trains, a per-thing record field on the main path. **The channel count is not model data.** The corpus names channels 0 to 3; `jungle/rides/totem` uses 3 and `jungle/sideshow/junspray` drives channels 0, 1 and 2.

**`FUN_00473490` does not survey the channels.** `ECX` and the role are loaded **outside** the loop and the pointer is never advanced, so it tests **channel 0** count-many times, and it **writes** as well as tests — refreshing channel 0's `+0x14`/`+0x18`/`+0x20` count-many times and answering "all finished" from channel 0 alone. **It is a shipped bug: port the bug.** A clean "all channels idle" port would invert the idle behaviour on multi-channel models. The consequence at the retrigger site: a Jungle Spray whose channels 1 and 2 are mid-clip is called finished and gets role 5 restarted on channel 0 beneath them. The 3-attempt cap in `FUN_00473c70` is load-bearing too: with no role 5 the predicate stays true for ever.

## Clip content: tracks, hide lists, rotation, visibility

| Address | What it is | Notes |
|---|---|---|
| `FUN_00470b60` | the key search | `param_4 == 1 -> 4` (position), `== 2 -> 0x14` (rotation), `== 4 -> 0x10` (UV). Returns a **normalised t** between two key frames, and **returns 0 when the frame precedes the first key**, so the engine skips the track rather than clamping to the first point |
| `FUN_00474840` | a plain cubic **Bezier**, not a spline | constants at `0x006fecb8` are 1, 3, -3, -6; expands to `P0 + 3(P1-P0)t + 3(P0-2P1+P2)t^2 + (-P0+3P1-3P2+P3)t^3`. The modulo wraparound is the **looping** arm |
| `FUN_00474bf0` | the straight lerp | |
| `FUN_00474cc0` | the third sampler (neither bit) | **used by nothing** |
| `FUN_004745c0` | the UV sampler | our sampler matches it line for line — **confirmed, do not re-derive** |
| `FUN_00471860` | the poser | assigns the node's **own local translation row** `node+0x40/+0x44/+0x48`; a node record's transform is a 4x4 at `+0x10`, so that row IS the translation, parent-local, exactly as a rotation key is. A model flagged `&4` at `+0x30` **adds** instead of assigning |
| `0x00471c83`-`0x00471d32` | rotation-key easing | see below |
| `0x006febe4` | the easing segment constant **8.999995231628418** | deliberately under nine so `t == 1` stays in the last segment |
| `0x006febec` | the easing byte scale, 1/255 | |

**What those samplers run over, for a vehicle, is not a track but the model's own path table** — the
array at model file `0xac`, whose records name a run of 12-byte XYZ points. A Bezier route's point count
is a multiple of three rather than `3n+1` because the loop is **closed**, which is what `FUN_00474840`'s
modulo arm above is wrapping. The scalar that drives it is a separate per-frame array on an animation
track setting channel `0x200`, and it is a **percentage**: space's slide runs exactly 0 to 100 and the
haunted house 0 to 200, which is two laps. It does not always rise — the ferry runs its route backwards.
Layout and counts are in the FileFormats clone under Models (`*.MD2`); they are not repeated here.

**Rotation keys carry an easing curve id** at key`+0x02` (`0xFFFF` = none) indexing an **8-byte** table at track descriptor `+0x34`, remapped through a **9-segment** ramp: **457 of 1,166 clips carry one**. Three things that matter: the id belongs to the **lower** key of the pair; the id is **not** the key's own index (**3,070 of 12,428 eased keys disagree**, ids reach 100, so the table is indexed not walked); and the ramp's endpoints are **implied** (0 before the first byte, 1 after the last), which is what makes it nine segments from eight bytes.

**The restore mask `0x289` includes position**, so unlike visibility a clip's position IS put back.

**Confirmed, do not re-derive:** morph decode-then-lerp is right; the engine's morph key cursor lives in **shared clip data** and a stateless search is the safe divergence; every clip declares first frame 0, so "elapsed from 0" and "absolute frame" are the same number. The morph channel-count rule is `+2` only when the descriptor's bit `0x2` is set, `+1` otherwise — **unmeasured, flagged not asserted.**

### A rotation key is the orientation inside the parent, not in the model

Confirmed against the data. A key replaces the mesh's **local** rotation; composing it against the mesh's *world* rotation throws the parent's turn away. **1,078 of the game's 2,592 rotation tracks target a mesh where the two differ**, so this is general.

**Every gate parents its doors to a root that carries no rotation**, where the two are the same matrix — which is why the lobby never showed it.

The proof is the construction clip, which by definition ends on the built object: Jungle Spray's ends with `fence` keyed 90 deg, `gun01` 180, `Puddle_01` 245, `tube_case01` 30 and `Bench` square — each exactly that mesh's **local** rotation, and each disagreeing with its world one.

### Node visibility: bit 0x10, set at runtime and never in the data

`FUN_00471860` applies an animation's visibility channel: it walks the entries **from the end**, takes the first whose absolute value is at or before the current frame, and sets 0x10 when that entry is `< 1` (**so a 0 entry hides**) or clears it otherwise. If no entry qualifies **the flag is left alone**, not defaulted to visible.

At draw time (`FUN_0045d090` and three others) the node walk tests `0x10` **after** computing the node's matrix and then carries on into the children, so **a hidden node does not hide its children** — bit `0x20` is what prunes a subtree. `0x80000000` protects a node from animation visibility altogether.

### The per-clip hide list

The list lives at clip block `+0x1a` (count) / `+0x38` (pointer). **854 of 1,166 clips carry one, 12,872 entries in total** (a later count over the same corpus says 859 clips carry one).

- **`FUN_00472d70` HIDES the incoming clip's list at clip start.** Each entry resolves by the same `idx < meshCount ? meshTable + idx*0xa0 : nodeTable + (idx-meshCount)*0x58` rule `ModelFile` uses, then sets flag `0x10` unless the node carries `0x80000000`. **Entries are NODE INDICES.**
- **`FUN_00472310` CLEARS `0x10` over the outgoing clip's list**, then restores `0x289` nodes from the master by copying matrix rows `+0x10/+0x20/+0x30/+0x40`, with arms for `0x1000` and `0x10000` and **no `0x20000` arm** — which is why visibility is never put back.
- The `& 4` id-search arm is a flag on the **MODEL record**, not on the clip. The default path is index-addressed and is all shipped data needs (**only 4 entries in the whole game fall past their model's nodeCount**).
- **`FUN_004726d0` recomposes hide state across all twelve channels** (array at `+4`, count at `+0xe`, stride 0x38), clearing then re-applying per channel.

**Measure its payoff in meshes, not entries: 12,090 of the 12,872 entries name transform-only nodes** (`'destroy'`, `'smoke'`, `'position01'`, `'kid_pos03'`) that carry no geometry. **778 name a real mesh**, and every one of those 778 is hidden by nothing else — no visibility track in the same clip ever names them. 635 of the 778 (82%) are advisor costume pieces, which `AdvisorModel.Dress()` already hides; the rest sit on things no shipped save places.

---

## The `.RSE` interpreter

### Dispatcher and word encoding

| Address | What it is |
|---|---|
| `FUN_00551cb0` | the dispatcher. Reads a dword, requires `(word & 0xff000000) == 0x80000000`, and dispatches only while `(word ^ 0x80000000) < 0x6a` — **0x6a is 106 opcodes** |
| `0x5567d8` | the dispatcher's jump table |
| `FUN_005587f0` | the `.RSE` loader |
| `0x765280` | `{name*, operandCount*}`, **8 bytes a record** — and the second pointer is the count as an **ASCII DIGIT STRING**, not an integer |
| `0x765880`-`0x765a48` | the opcode names, which run **downwards** as the index rises |
| `DAT_00879190` | the version the loader compares the header against; a mismatch only **warns** |
| `0x00551cc1` | `XOR EDI,EDI` — **EDI is zero throughout the dispatcher** |
| `0x551cd7` | the prologue's `EBX = 0xffffd8f0` (-10000), the "park the PC" value |

Outside the opcode range the engine says *"RSSE: Unknown instruction"*; without the 0x80 byte, *"RSSE: Bad instruction - missing p..."*.

Decoded table head: `NOP "0"`, `CRIT_LOCK "0"`, `CRIT_UNLOCK "0"`, `COPY "2"`, `SETLV "1"`, `SUB "3"`, `ENDSLICE "0"`, `GETTIME "1"`, ... — exactly `Opcode.cs`'s own order (NOP=0 ... GETTIME=7), so the enum and the binary agree. **`SETLV` takes one operand.**

Of the published docs' 105 opcode sections, **39 are real stub sections** (105 was a count of *lines* matching "unknown", and a stub spends two lines on the word). The binary has **106**: `CRIT_UNLOCK` is **missing entirely** from the docs, between `CRIT_LOCK` and `COPY`, and `SINGLESCREAM` is misspelled `SINGLESREAM`. The claim that the instruction set has **210 opcodes is wrong** — it cites a website; the binary says 106.

**The body is a flat dword array, not a byte stream.** The PC (`+0x3c`) indexes it directly against the length (`+0x50`) — the dispatcher and the loader agree on both fields independently. Every word carries its kind in its **top byte**:

    0x80  opcode        0x40  variable index      0x20  branch target (an ABSOLUTE WORD INDEX)
    0x10  string offset into the blob             0x00  literal

**A branch needs no arithmetic.** Proof: **2,664 of 2,664** branch targets in the corpus land on a word the instruction walk independently calls a start, and **330 of 330** string operands land on a real string.

### The two helpers an interpreter needs

| Address | What it is |
|---|---|
| `FUN_00557a70` | **fetches the next word**: bounds-checks the PC at `+0x3c` against the length at `+0x50`, reads `[+0x18][pc]`, advances; on failure logs *"Tried to read outside script"* and parks the PC at `0xffffd8f0`. Most handlers inline this rather than calling it |
| `FUN_005573a0` | **resolves an operand to a value** |

    if ((word & 0xff000000) == 0x40000000) return variables[word * 4];   // tag 0x40
    return (int)(short)word;                                            // everything else

**The `* 4` discards the tag as it scales the index** — `0x40000000 * 4` overflows to exactly 0 — and the fallback is a **signed 16-bit** read. So sign-extension is not a `COPY` quirk, it is the generic rule for every value operand; opcodes wanting a string or a label (`NAME`, `BRANCH`) check the tag themselves and keep the full 24-bit field instead. `COPY`'s literal path does `MOVSX EAX,AX` at `0x00551df5`, so a literal is the **low 16 bits, sign extended** — no shipped literal reaches past 16 bits.

### Arithmetic, the destination rule and the result register

**The destination is operand 0, and it must be a variable.** Read off the shared store tail at **`0x00554911`**, which `SUB` jumps to: it stashes the result in the scratch field `+0x48`, tests operand 0 for tag `0x40`, and **if it is not a variable the instruction simply RETURNS, doing nothing**; otherwise it writes `variables[op0] = result`. `ADD` does the same inline.

    ADD <dest:var> <value>            variables[dest] += value        handler 0x00553e13
    SUB <dest:var> <a> <b>            variables[dest] = a - b         handler 0x00551e58
    DIV <dest:var> <a> <b>            variables[dest] = a / b         handler 0x00553f8a
    MOD <dest:var> <a> <b>            variables[dest] = a % b         handler 0x0055405c

**A zero divisor does not trap**: `DIV` and `MOD` compare the divisor against zero first and yield **0** (`0x00554127`), then store as normal. They share one tail from `0x0055412a`; `IDIV` runs once and `DIV` keeps EAX while `MOD` keeps EDX.

**`SUB`, `DIV` and `MOD` all put the destination FIRST, and the published docs put it last.** The docs say `SUB/DIV/MOD <value> <value> <dest>`; the binary resolves operands **1 and 2** to values and stores to operand **0**. **`ADD` is the one the docs get right** (`<source>` doubling as the destination). **None of ADD, SUB, DIV or MOD touches the flags** — they write only the scratch field `+0x48` — so the docs' "the relevant flags will be set", repeated on all five arithmetic entries, is false.

**There is no flags register. `+0x48` is a last-result register, and the branches test it.** Every instruction that computes something writes its result to `+0x48` **unconditionally**, and only then writes `variables[op0]`, and only if operand 0 carries tag `0x40`. The conditional branches compare `+0x48` against zero, **signed**:

    BRANCH_Z   0x00553afb  CMP [+0x48],0 / JNZ nop   -> taken when result == 0
    BRANCH_NZ  0x00553bf1  CMP [+0x48],0 / JZ  nop   -> taken when result != 0
    BRANCH_NV  0x00553b4d  CMP [+0x48],0 / JGE nop   -> taken when result <  0
    BRANCH_PV  0x00553b9f  CMP [+0x48],0 / JLE nop   -> taken when result >  0   (STRICTLY)

    TEST <var>           +0x48 = variables[op0]                     0x00554152
    CMP  <var> <b>       +0x48 = variables[op0] - value(op1)        0x005541a5
    MULT <dest> <a> <b>  variables[dest] = a * b, tail 0x00555b48

So `CMP a, b` then `BRANCH_Z` is "branch if equal", and `TEST v` + `BRANCH_NZ` is "branch if v is non-zero". **`+0x48` is the whole condition state** — no Sign bit, no Zero bit. **`CMP` is a SUBTRACTION, not a bitwise AND** as the docs page says: `SUB ECX,EAX` then `MOV [EBP+0x48],ECX` at `0x00554235`.

**`0x005567b4` is NOT an error exit — it is NOP's own handler.** It sits inside the dispatcher and is a bare epilogue (`POP EDI/ESI/EBP/EBX; ADD ESP,0x134; RET`). `END` is the instruction **three bytes earlier** at `0x005567b1`, which parks the PC first.

**Two different bail targets, and the difference matters.** A branch whose condition is false falls to `0x005567b4` (NOP — harmless, carry on). A branch whose operand is **not** tagged `0x20` goes to `0x005567b1` (END — **parks the PC**, stopping the script). Same for a destination that is not a variable: NOP, not END.

That silent no-op explains **the 17 shipped instructions whose operand 0 is a literal**, which look alarming: `MOD literal:0 variable:2 literal:9` **identically in three themes** (jungle/hallow TourRide, space scitour), `RAND literal:0 literal:N` four times, `SETVARINCHILD literal:0/1` seven times, two `SUB literal:0 ...` in space/orbiter. They are compiler artefacts the engine quietly ignores, not corruption and not a misaligned walk. **An interpreter must ignore them the same way rather than throwing.** (The reachability check run over them proves nothing either way, because it follows both sides of every conditional and treats every fallthrough as taken.)

### The two stacks

**One stack array, two stacks, growing toward each other.** Both share the base at `+0x20` and the size at `+0x54`, and each has its own index:

    +0x40   JSR / RETURN / PUSH / POP    starts at stackSize-1, JSR DECREMENTS (grows DOWN)
    +0x44   HUSH / HOP                   starts at 0,           HUSH INCREMENTS (grows UP)

`JSR` (`0x005539a9`) takes the PC — which already points past its own operand — **ORs in the `0x20000000` label tag**, writes it to `stack[index]`, then **decrements** the index. `RETURN` (`0x00553a32`) **increments** first, reads `stack[index]`, checks the tag is `0x20` (else NOP) and untags it into the PC. Both refuse when there is no stack or the index leaves `0 .. stackSize-1`.

`HUSH` writes `stack[+0x44]` and increments; `HOP` decrements and reads, landing in the shared store tail `0x00555939` (the same one `POP` uses: result register always, then `variables[op0]` only if operand 0 is a variable). **`HOP` bounds-checks `+0x40` against the size as well as its own index** — a collision guard between the two stacks, which is what gives the layout away. `+0x44` needs no initialiser because the loader zeroes the whole script object.

The loader's otherwise unexplained `field[0x10] = field[0x15] - 1` is `index = stackSize - 1`, the initial top of a downward stack (`0x10 * 4 = 0x40`), and `field[0x15]` is the header's **stack size**.

**The engine's call stack is LIFO.** A `Queue<int>` with Enqueue/Dequeue is FIFO and returns a nested call to the *first* caller, not the most recent — that is an inherited bug, not the engine's behaviour.

### The scheduler — who gets a turn and when

`FUN_005516b0` is the RSSE tick and the **only** caller of the dispatcher (`0x0055171f`). It has 17 call sites: `Game_StateMachine` at `0x0054f56b`, and the layout replay `FUN_004041d0` sixteen times, in two runs of eight at `0x00404482`-`0x004044a5` and `0x0040455a`-`0x0040457d`. Every field below is read, not inherited.

    tick counter    DAT_008791a4     incremented once per tick, before any script runs
    script list     DAT_008791b0     doubly linked registry, newest at the head; next pointer at offset 0
    initialised?    DAT_008791a0     zero -> logs "RSSE: Need to initialise before you can tick" and bails
    critical flag   DAT_0087919c     RESET TO 0 AT THE START OF EVERY TURN (0x00551701, per script)

    field 2    +0x08   the script's id - the SAME id COAST_INITIALISE matches on
    field 3    +0x0c   id of a script to mirror the speed word into
    field 6    +0x18   the body
    field 0xf  +0x3c   the PC; END parks it NEGATIVE, which is what ends the turn
    field 0x25 +0x94   the time slice (50 in every shipped file)
    field 0x26 +0x98   the budget for this turn, refilled from 0x25
    field 0x30 +0xc0   the speed word, a SHORT
    field 0x38 +0xe0   the ride handle COAST_INITIALISE stores
    byte       +0xb8   TURBO

    run this script if  byte[+0xb8] != 0   OR   ((scriptId ^ tick) & 7) == 0
    budget = field[0x25];  field[0x26] = budget
    while (budget > 0 && PC >= 0) { dispatch one instruction; if (!critical) budget--; }
    if (PC < 0) tear the script down (FUN_00559060)

**A script gets a turn only every eighth tick, staggered by its id** — unless `+0xb8` is set, which makes it run every tick. **`TURBO` is what sets that byte** (`0x005542b9` writes `[EBP+0xb8]`), so TURBO means "run me every tick", not "run me faster". **`TURBO` stores the RAW low byte of its operand word**, not a resolved value — harmless because all 20 shipped uses are literals, and they are exactly **ten `1`s and ten `0`s**, a boolean confirmed by the data.

The budget is decremented **only while the critical flag is clear**, and the flag is read **after** the instruction has run (`0x00551724`-`0x00551730`): `CRIT_LOCK` itself costs nothing, and a lock reached with one unit left still runs its section in that turn, up to the unlock or a yield. **The time slice is an INSTRUCTION BUDGET, not a duration.**

**Nothing else bounds the loop** (`0x00551719`-`0x0055173c`): it ends only when the budget is spent or the PC goes negative - no instruction count, no clock, no watchdog, no tick or frame deadline - and no callee of the dispatcher writes the flag or the budget. So a section that branches back without reaching `CRIT_UNLOCK`, a yield or a negative PC hangs the game thread. The flag is reset once per script turn, after the stagger and body tests, not once per tick. A time slice of nought or less skips the loop and leaves the script alive; every shipped file says 50.

| Address | Opcode | Behaviour |
|---|---|---|
| `0x00551d44` | `CRIT_LOCK` | `MOV [0087919c],1` — instructions stop costing budget |
| `0x00551d59` / `0x00551d5f` | `CRIT_UNLOCK` | `MOV [0087919c],0`, then `MOV [+0x98],0` — **and ends the slice on the way out** |

So a locked region runs **atomically, unbounded by the 50-instruction slice**, unless it yields (seven shipped sections do - see "Corpus shape"), and **leaving it yields** — that second instruction stops a script monopolising the loop after unlocking. **`CRIT_LOCK` is a VM critical section, not a ride lock**; the docs page's "locks a ride, preventing visitors from accessing the ride" is wrong.

**`ENDSLICE` and `WAIT` yield by zeroing the budget**, which is why `ENDSLICE`'s whole body is one instruction: `MOV [+0x98], 0`. `field[0x26]` and `+0x98` are the same field.

**Nine handlers write the budget, and all nine write nought** (every one of the 106 jump-table targets walked to its `RET`; `search_bytes` on the flag's address finds its six references): `CRIT_UNLOCK`, `ENDSLICE`, `WAIT` and `WAITABS` (`0x005538d7`, a rewind of two), `WAITANIM` (`0x00552a78`, `0x00552b0b`), `WAIT4ANIM` (`0x0055391a`), `TRIGWAITANIM` (only on a re-entry that is still waiting; its first visit, `0x005536a0`, rewinds WITHOUT a yield, so the same turn dispatches it again), and the two channel forms. **`WAITANIM_CH` and `TRIGWAITANIM_CH` rewind one word short**: `0x005532f7` subtracts 3 from a four-word instruction (and `0x005536a0` 4 from a five-word one), so the next dispatch lands on operand 0, prints *"RSSE: Bad instruction - missing parameter ?"* and parks the script. Nothing ships them, nor `WAITABS`. **The critical flag has two writers among the handlers**, `CRIT_LOCK` and `CRIT_UNLOCK`; the scheduler's reset at `0x00551701`, the initialiser `FUN_00551600` and the save-state reader `FUN_005597a0` are the others, and neither of those two is in the handlers' callee closure.

**A script's death frees its records, and the trigger is a negative PC.** Every operand-fetch overrun stores `0xffffd8f0` into the PC at `+0x3c`, and the teardown `FUN_00558500` drains the effect list. **Running off the end of the body is the normal death path**, and it prints *"RSSE: Tried to read outside script memory"* on the way — **so that string is not evidence of a malformed script.**

### The clock, the speed word, and WAIT

**The clock is milliseconds.** The chain is `0x00402d70` -> `0x00402f10` -> `0x004030d0` -> `0x004033a0` -> `0x005f5fa0` -> **`0x005f5f10`**, and that last one is the whole answer: it calls `QueryPerformanceCounter` and divides by a stored divisor, and **falls back to `timeGetTime()`** when the counter is unavailable. The two paths are chosen at runtime and must be interchangeable, so the divisor scales QPC to `timeGetTime`'s unit — milliseconds. `WAIT 3000` is three seconds.

The dispatcher's prologue computes `[ESP+0x14] = DAT_00700fd0 - (short)field[+0xc0] * DAT_00700fcc`, **and that is the divisor `WAIT` FDIVs its duration by**. `DAT_00700fcc` = **-0.01**, `DAT_00700fd0` = **0.5**, so the divisor is `0.5 + 0.01 * speed`. **At the loader's speed of 50 that is exactly 1.0** — the neutral point — which is why the two 50s look like one thing and are not.

**Two 50s that are NOT the same field, and must not be conflated:** the header's time slice is `field[0x25]` (`+0x94`), while `field[0x30]` (byte offset `0xc0`) is the speed the loader sets to 50 itself.

**Three writers of the speed word `+0xc0`, and only two are inside the RSSE subsystem:**

| Address | What writes it |
|---|---|
| `0x00558c3c` | the loader, `MOV word ptr [EBP+0xc0],0x32` — the hardcoded 50 |
| `0x00551888` | the scheduler, copying it into the linked script named by field 3 |
| `0x0055a30d` | `FUN_0055a300(frame, value)`, a plain setter, called from **outside the RSSE subsystem**: the object constructor `FUN_004db090` at `0x004db54a` finds the script by the id it has just stored at the thing's `+0x24` and pushes the item's own operating speed in, logging `SPEED = %d` |

**No opcode writes it.** A sweep of `0x00551000`-`0x0055a000` (loader, scheduler, dispatcher, every handler) finds **exactly two** writers — the first two rows above; the third sits **past the top of that window**, which is why a scan bounded by the RSSE subsystem misses it. The constructor does the same for a duration through `FUN_0055a0b0(frame, 3, value)`, which writes **variable index 3** — `VAR_DURATION` — and logs `DUR = %d`.

So **"speed is 50 for every script that ever runs" is false**: it is 50 for a script nothing binds, and whatever the item says for a script bound to a placed object. The divisor `0.5 + 0.01 * speed` is neutral only in the first case.

**What is not proven and must not be guessed: which `.sam` key feeds either of them.** The constructor reads its record through an `undefined2 *`, so the `+0xd4` and `+0xd0` the decompiler prints are **element** offsets — byte offsets `0x1a8` and `0x1a0`. Those do not line up with where `FUN_004db7d0` parses `mOperatingSpeed` and `mOperatingDuration`, which land at `+0x58` and `+0x5c` of whatever record *it* is filling. Two readings that disagree are not a mapping.

**`WAIT <duration>` blocks across ticks without a thread.** First execution: resolve the duration, read the clock (`FUN_00402d70`), compute `now + duration/scale`, store it as a **deadline at `+0xa0`**, **rewind the PC by 2** so the same WAIT runs again, and zero the budget. Later executions take the other path (`+0xa0 != 0`): re-read the clock, and if `now < deadline` keep waiting, else **clear `+0xa0` and fall through** — the PC is already past the operand, so execution simply continues. **An interpreter must model WAIT exactly this way** — as a deadline plus a PC rewind — rather than as a sleep, or scripts will not resume correctly.

**`WAIT` and `WAITANIM` share `+0xa0`, and the engine's "empty" test is NONZERO** — so an interpreter holding the deadline as a plain float with `<= 0` meaning empty reads a past deadline as an empty slot, arms it again every visit, and the script never moves.

The other time opcodes:

    GETTIME  (7)  00551f1e  resolve dest, read clock, store RAW - nothing per-ride, nothing subtracted
    WAITABS (45)  00553828  first visit stores clock + operand, unscaled by speed (0x005538cb), then
                            rewinds and yields like WAIT and shares its compare (0x00553859); ZERO shipped uses
    SETTIMER(95)  0055641b  field[+0xc4] = clock + resolve(operand)  -  NOT speed-scaled, unlike WAIT
    GETTIMER(96)  0055644d  result = field[+0xc4] - clock, floored at 0 by the JNS at 0x0055646d

**`GETTIME` is NOT "how long the ride has been alive"** — that was the published docs' claim and it is wrong; it stores the shared clock verbatim. 172 uses / 57 scripts. **SETTIMER 40 uses and GETTIMER 21, across the same 18 scripts**, and all 21 GETTIMERs name a *literal* destination, so the answer lands in the result register and the write is skipped. **WAITABS has zero uses in all 308**, so it was deliberately left unimplemented.

### `RAND` (28) at `0x00553932` — its bound is NOT resolved

The generator at `0x00516330` is an LCG: `state = state * 0x19660d + 0x3c6ef35f`, rotated right 13, made positive. The opcode then does `SHR 1`, `abs`, `MOVSX ECX,DI`, `INC ECX`, `IDIV`, **takes the remainder**, and `abs` again — so the range is **0 to the bound INCLUSIVE**. **The `MOVSX` is bare**: there is none of the `AND 0xff000000 / CMP 0x40000000` tag test every resolved value operand gets, so a bound tagged as a variable would be read as its own index. All 56 shipped uses name a literal (bounds 1-10 plus one 300 and one 5000), so the difference is invisible in the data and would have been wrong in code. **The seed is not established** — `FUN_00516370` is a plain setter called from eight places, none of them the script system — so a faithful implementation reproduces the arithmetic, not the numbers.

### The animation opcodes

Opcode numbers: 15 FLUSHANIM, 16 TRIGANIM (3 operands), 17 WAITANIM, 18 LOOPANIM, 19 TRIGWAITANIM, 20 GETANIM, 21 TRIGANIMSPEED, 22-27 the `_CH` variants (one extra operand, the channel), 46 WAIT4ANIM — all funnelling into **`FUN_004732a0`**.

**Every one tests the model handle at `+0xc8` first** — an index into the table at `0x7a4610`, nought when the script has no model. That null path is completely defined. The trigger is `FUN_004732a0(model, animation, parameter, loopFlag, divisor, 0)` returning a length in ms, and `loopFlag` is **0 for `TRIGANIM`, `WAITANIM` and `TRIGWAITANIM`, 1 for `LOOPANIM`** — so **`WAITANIM` really does start the animation**, which the docs page already said.

    FLUSHANIM(15)    00552861  model==0 -> leave; otherwise the body at 00553053 (NOT read)
    TRIGANIM(16)     00552875  len = trigger(); len -= 300; floor 300 SIGNED (JGE); store in operand 3
                               then 00552fe5: +0xa4 = clock + len/divisor, +0xa8 = 0xffff
    WAITANIM(17)     005529bc  same trigger - but its deadline goes in +0xa0, WAIT's OWN field
    LOOPANIM(18)     00552b2f  key = (op2 << 16) + op1; if key == +0xa8 LEAVE doing nothing;
                               else trigger(loop), +0xa4 = 0, +0xa8 = key
    TRIGWAITANIM(19) 00552c1a  TRIGANIM plus +0xbc = 1 and a rewind of 4 onto itself
    WAIT4ANIM(46)    005538eb  +0xa4 only

**`FLUSHANIM` stops nothing.** `FUN_00473270` writes the sentinel 12 into `channel+0x24` — the role *queued* to play next. The clip actually running is untouched and plays out, and nothing is answered.

**`WAITANIM` is NOT `TRIGANIM` with a wait, and that is the trap.** It stores `len - 300` as the **low half of a qword whose high half is nought** and reads it back with `FILD qword`, so `-300` arrives as 4,294,966,996; `__ftol` (`0x0067a830`) is a `FISTP qword` handing back the **low dword**, so nothing overflows and `-300` comes back out; and the 300 floor is then compared with **`JNC`, unsigned**, which a negative passes where `TRIGANIM`'s `JGE` catches it. **So with no model `WAITANIM`'s deadline is `clock - 300`, already past, and the instruction costs exactly one turn** — it still yields, because the engine sets the deadline without looking at it. It does **not** "wait 300 ms".

`WAITANIM` does trigger the clip: `0x00552a95` tests the model handle and `0x00552ab0` calls `FUN_004732a0`; the no-model path is `XOR EAX,EAX`. **Three of Lost Kingdom's eight items run only `WAITANIM`** — Staff Room, Security Camera, Litter Bin — so a passive wait leaves them inert.

**Two timing asymmetries.** Both `TRIGANIM` and `WAITANIM` divide by the speed divisor (`0x00552acd` is `FDIV float ptr [ESP+0x14]`, the same divisor, worked out afresh per instruction as `0.5 + 0.01 * speed`, exactly 1 at the 50 every script carries). The real asymmetry is the **order and the signedness**: `TRIGANIM` floors at 300 **signed** (`JGE`) and *then* divides; `WAITANIM` divides and *then* floors at 300 **unsigned** (`0x00552ad6` `CMP EAX,0x12c` / `JNC`). `LOOPANIM` is idempotence-guarded on the key at `+0xa8` (which the triggers set to `0xffff` precisely to break that guard) and discards its length.

**`WAIT4ANIM` waits on a second deadline field** (`+0xa4`, `0x005538eb`) — **not** `WAIT`'s twin. Two differences, both load-bearing: it never arms anything itself, and **when `+0xa4` is nought it leaves at once without waiting at all**.

**The word at `+0xe4` is set to `0x3e8` (1000) by all four triggering handlers and is not identified.**

**`TRIGWAITANIM` (19) at `0x552c1a` — built 2026-09-20, once models existed; OpenTPW reproduces the model path and, as a declared deviation, steps over the model-less one counted rather than parking.** First visit (`+0xbc` is 0): trigger exactly as `TRIGANIM` does, then `0x553693` does `INC EDI` / `+0xbc = EDI`, so the mark is the **animation id PLUS ONE** and 0 means "not armed"; then `+0x3c -= 4` onto itself and return **without** `+0x98 = 0`, so the same turn re-enters it. Re-entry: with a model, `FUN_00473fb0` gives channel 0's entry[1] — and that function is a **plain accessor**, `*(model+0x10) + channel*0x38` returning entry[0] and writing entry[1] out, **not a "channel cursor"** — then `INC` and compare against the mark. Equal -> `0x5535f4` clears `+0xbc` and falls through; not equal -> `0x5535d6` rewinds 4 **and** sets `+0x98 = 0`. **With no model the accessor call is skipped and the comparison is made against the RAW THIRD OPERAND**, so it parks for ever unless op3 == op1 — which **none** of the 133 shipped uses satisfies (132 differ, 1 is a variable). `TRIGWAITANIM_CH` at `0x55359b` shares `+0xbc` and has the identical shape, which is independent agreement on the decode.

**`GETANIM_CH`'s operands are `(destination, channel)`**, not `(role, entry)` — **the opposite shape to every other `_CH` instruction.** Counting its first operand as a role inflates any role census. The corrected corpus figures are **627 distinct (item, role) pairs and 806 distinct (item, role, entry) references, all 806 resolving to a shipped file bar the eight known absences**, with the highest entry index any script asks for being 9.

**No opcode hides a mesh of the base model**: ADDOBJ/KILLOBJ/FADEOBJ and the LIMBO family act on the script's own object table, not on model nodes. Variable names like `VAR_LETMEON` and `VAR_LANE1` appear nowhere in the exe — **they are data**. There is no "lane" in the exe; `VAR_LANE1..3` are variables inside the item's own `.RSE` script. `TRIGANIM` uses channel 0 and the `_CH` opcodes take one, so a sideshow's several clips run at once on separate channels.

**Corpus:** `WAITANIM` 547 uses / 250 scripts — **the first instruction to block 183 of the 308**; `LOOPANIM` 210/114; `TRIGWAITANIM` 133/56; `WAIT4ANIM` 170/75; `TRIGANIM` 74/59; `FLUSHANIM` 15/7. The `_CH` variants are rare (`TRIGANIM_CH` 63/6, `GETANIM_CH` 15/5, `LOOPANIM_CH` 1) and `GETANIM` is unused.

### The effect subsystem

`ADDOBJ`, `EVENT`, `KILLOBJ` and `FADEOBJ` do **not** add "objects to slots": they start **particle effects and sounds**, and the "slot" is a **kill tag**.

    ADDOBJ(8)       00551f5f  malloc 0x1c, link at frame +0xb0, DAT_008791b4++, 4 operands,
                              then FUN_00557970(frame, record, op3, 1000, op4)
    ADDOBJ_EXT(9)   005520e8  the same, 5 operands - op4 is a LIFETIME where ADDOBJ hardcodes 1000
    KILLOBJ(10)     005522c5  walk +0xb0; every record whose +0x18 == operand is stopped and freed
    FADEOBJ(11)     005523d0  the same walk; differs ONLY for a sound
    EVENT(13)       00552615  3 operands -> FUN_005573d0(frame, op1, op2, op3, 1000); handle DISCARDED

**The 28-byte record:** `+0x00` next, `+0x04` prev, `+0x08` type, `+0x0c` the spawn's handle, `+0x10` node, `+0x14` the node's index on the model, `+0x18` the tag. `DAT_008791b4` is a live count of them.

**`FUN_005573d0` is the whole subsystem**, switching on the type: **1-2 `Particles_Spawn` / `Particles_SpawnFull`; 3-9 `Sound_PlayEffect` through a named category** — 3 `cat_rides'`, 4 `cat_ambient'` (a SECOND pair registered by `FUN_0051ec50`), 5 `cat_rides`, 6 `cat_kids`, 7 `cat_staff`, 8 `cat_ambient`, 9 `cat_ui`, all from `Sound_RegisterGlobalCategories`; **10** the thing's own custom bank, which returns 0 early if none is registered. Anything else is **"RSSE: Unknown object type"**, and the guard is unsigned after a `DEC`, so 0 and negatives take it.

**The no-model path does not do nothing — this is the opposite of the animation family.** `FUN_00556b90` is the position gate, and with the model handle at `+0xc8` nought it **returns 1**, not 0, having written position `(0, 0, K)`. Its **only** `return 0` is "RSSE: Invalid Node ID", reachable only when a model exists. **So the engine really does spawn, at the origin.** Two further traps in that gate: it **never writes the direction vector** on the fallback path, so type 2 with no model reads **uninitialised stack** in the original; and the same block also serves a live case (model present, node negative) where it returns a **real** position.

**`KILLOBJ` kills EVERY match, not the first.** At `0x552376` it does `MOV EAX,ESI / MOV ESI,[ESI]` — advancing *before* unlinking what it matched — then rejoins the test at `0x5523bd`. There is no break anywhere in it, and even the unknown-type error path converges on the same unlink-and-continue. **`FADEOBJ` is identical for a particle** (both reach `FUN_0051ff70(handle, -2)`, which is `Particles_Kill`'s own body) and differs only for a sound: `Sound_Stop` vs `Sound_StopFading`.

**`EVENT` keeps no record at all**, so nothing it starts can ever be stopped — that asymmetry is the only difference between it and `ADDOBJ`, and why `EVENT` has no fourth operand.

**The effect id is NOT range-checked.** No shipped script names a particle above 95, but `FUN_005573d0` tests the id for **bit 15** and remaps it through `FUN_004145d0`. A corpus range is not an engine invariant.

**The record is a live binding, which the handlers alone do not show.** `FUN_005516b0` walks the `+0xb0` list **every tick, for every script**: `0x55188f MOV EDI,[ESI+0xb0]`, advancing at `0x551ab2`, dispatching per record on `+0x8` through a byte map at `0x551b18`. For each it **re-reads the model at `+0xc8`, re-resolves the node through the cached index at `+0x14`, and pushes the position back into the particle or sound system through the handle at `+0xc`.** So `ADDOBJ` is not "spawn and keep a handle" — the record is what glues a playing effect to a moving ride, and that is the shape any world-side adapter must take.

**Other writers of the `+0xb0` list**, so nothing here reads as ADDOBJ's private property: `ADDOBJ_EXT`; the **save-state reader** `FUN_005597a0`, which rebuilds it from an `"OBJ "` section (no shipped `.RSE` has one — they all begin `RSSE`); the teardown `FUN_00558500`; and `FUN_005516b0` itself.

**A tag is a GROUP label, not an object id.** 95 of the 308 files give one tag to several `ADDOBJ`s — `AnimCtrl.RSE` (and its hallow/jungle/space twins) uses tag **1 at 24 separate sites**, `ccride.RSE` tag 10 six times. So `KILLOBJ`'s kill-every-match is the ordinary case, not a corner. **Tag 0 is legitimate data** and must never be a "no tag" sentinel — and the engine's own overrun path synthesises operand 0 (`XOR ECX,ECX` after the `0x765ca4` complaint), which then kills every record tagged 0.

**Two defects not to copy, and one limit not to invent.** The node cache at `+0x14` is used as an array index with **no test for its own `0xffffffff` failure value**, so a non-negative node id the model does not have reads out of bounds. **Nothing ages or reaps a record** whose effect has finished, so a record can outlive its effect indefinitely while the tick loop keeps pushing positions into a dead handle — the original survives only because a handle is slot-plus-generation (slot max `0x77`) and every consumer validates it. And the global `DAT_008791b4` has **no cap anywhere**: every read is followed by an `INC`, a `DEC` or a return, with no comparison against a maximum.

**Unexercised by shipped content:** `ADDOBJ` never uses type 6 or type 10, and `EVENT` never uses 7-10, so the whole custom-sound-bank path (`FUN_004145e0`) is decoded but untested by any shipped script.

**Corpus:** `ADDOBJ` 644 uses / 164 files, `EVENT` 527/107, `KILLOBJ` 235/122, `FADEOBJ` 113/39, `SETOBJPARAM` 20, **`ADDOBJ_EXT` and `EVENT_EXT` 0 uses**. Node `-1` is the commonest operand in the whole corpus (277 of 644, 341 of 527). Tags run 0-1000 and two tags are killed that no `ADDOBJ` creates. Particle types use ids 1..95; sound types 6..220.

### `SETOBJPARAM` (12) and `DIPMUSIC` (104) — both buildable with no world

`SETOBJPARAM` at `0x005524d9` is `<tag> <param> <value>`, walking **`+0xb0`** — the same record list `ADDOBJ` fills — and matching the record's tag at `+0x18`. Type dispatch (byte map `0x5569c4`, jump table `0x5569b8`) has **two cases only: particle types 1-2 do nothing**, sound types 3-10 call `FUN_0051bc40(record+0xc, param, value)` and store the answer back at `+0xc`. **That call returns 0 when the sound system is down** (`DAT_00802bc8`/`DAT_00802bd4`, written only by the sound init and its teardown), so with no world the record's handle becomes nought — the engine's own answer. All 20 uses are in `bus.RSE`, **one copy per theme — four scripts, not one** — as `(1, 20, 0)` x12 and `(1, 20, 75)` x8. It is a **third reader** of the list and rewrites `+0xc`, the handle itself. **Its byte table is `[0,0,1,1,...]`, NOT `KILLOBJ`/`FADEOBJ`'s `[0,1,2,2,...]`** — do not carry the one over to the other. It is a no-op on a particle record. The handler has not been read in full.

`DIPMUSIC` at `0x005564c0` resolves its operand, stores the low byte at frame **`+0xb9`**, and calls `FUN_0051e710`, which is only `DAT_00803ac8 = 1; DAT_00803ad0 = value`. **The frame byte is its lifetime**: `FUN_00558500` tests `+0xb9` and calls `FUN_0051e710(0)`, so a script's death un-dips the music. All 8 shipped uses pass a literal 1.

### `REPAIREFFECT` (93) at `0x005562a6` — unbuilt; it falls to the counted dispatch default

One operand, shipped **70x value 1 and 70x value 0**. It does **not** block — no rewind, no `+0x98`, and its only unusual exit is the `NOP` handler — so this is a different failure from `TRIGWAITANIM`: **it would take an access violation rather than park.**

    FUN_00556de0(frame, value)   value==0 -> if +0xcc, stop that particle and clear +0xcc.
                                 value!=0 -> GATED on the thing handle +0xac != 0; looks the thing
                                 up, takes its particle id at +0x68, resolves a position from the
                                 model at +0xc8, Particles_Spawn, stores the handle at +0xcc.
    then value!=0 (0x5562cd)     UNGATED: thingTable[+0xc8] -> FUN_00556a80 -> Sound_PlayEffect
                                 twice, sample ids 0x6f (111) and 0xd3 (211).
    then value==0 (0x55635f)     needs +0x8c > 5, reads VARIABLE 5, then UNGATED as above ->
                                 FUN_00551320, which picks a sample from 0x4b..0x5a by variable 5.

**`+0xcc` is the repair particle's handle** — a frame field, one slot, and `FUN_00558500` clears it on death. The killer is `FUN_00556a80`: it does `*(*(*(arg + 8) + 4) + 0x78)` with **no guard**, and its argument is `thingTable[frame+0xc8]` at `0x7a4610` — a table filled at runtime, indexed by the script's model handle with no guard. The sound side is safe (`FUN_0051bfc0` is `Sound_PlayEffect`, guarded on `DAT_00802bc8`/`DAT_00802bd4`), but that does not rescue it.

### The limbo subsystem — and it needs no world

Five handlers, every one working on the script's **own frame** rather than on anything outside it:

    LIMBO(58)        00554de5  2 operands, both resolved. capacity<=0 -> answer 0. else scan +0x24 for
                               the first slot whose handle is 0; none -> 0. else slot = {op1,
                               clock + op2*1000}, +0x60++, ANSWER 1. Writes NEITHER operand.
    UNLIMBO(59)      00554ef0  first slot with handle!=0 AND [slot+4] < clock (signed JL) -> 0x5567bf:
                               +0x60--, take handle, zero slot, Store into the operand. none -> 0.
    FORCEUNLIMBO(60) 00554f74  TAG TEST FIRST: not a variable -> NOP tail, nothing at all, not even the
                               result register. else the first occupied slot, whatever the clock says.
    INLIMBO(61)      00555012  answers +0x60.
    LIMBOSPACE(62)   00555045  answers +0x58 - +0x60. All 24 uses pass a literal 0.

**The frame's limbo state:** `+0x24` the slot array, `+0x58` how many slots there are, `+0x60` how many are taken. A slot is **eight bytes** — the handle, then the clock reading they are due back at. **`+0x58` is read straight out of the file header at offset `0x14`** by `FUN_005587f0`, which then allocates `count * 8`; `FUN_00558500` frees it beside the variables and the stack.

**24 of the 308 shipped scripts declare slots, every one of them 10, and those 24 are exactly the scripts that use a limbo instruction — no row disagrees in either direction.** Shops, toilets and arcades, not rides. **`LIMBO`'s second operand is SECONDS**: three chained `LEA`s make 125 and the `[EAX + ECX*8]` makes it 1000.

**One engine defect, to be reproduced deliberately rather than quietly fixed:** limboing a handle of **nought** writes it and counts it, but a slot holding nought is precisely what the engine calls free — so the tally and the slots part company and that guest can never be found again. No shipped script can reach it; all 24 guard the instruction with a test of the variable that would name the guest.

### The script-to-script family

Ten handlers, and **every one reaches another script by ID through one global registry** — no script ever holds a pointer to another.

    SPAWNCHILD(63)     00555081  string operand only. Path = frame +0x38 (own directory) + operand.
                                 Loader answers an ID; +0x0c = it; guarded by TEST/JZ. Then finds the
                                 child and copies parent +0x8 -> child +0x10, plus +0xac(word), +0x9c,
                                 +0xb4, +0xc8. WRITES NO RESULT. Does NOT kill the child it replaces.
    SPAWNSOUND(64)     005551ab  same prologue and loader call; stores into +0x14 and stops. NO link
                                 of any kind, and NO guard - a failed load writes 0 into the slot.
    REMOVECHILD(65)    00555263  +0x0c != 0 -> FUN_00559060(id, 0), then +0x0c = 0.
    SETVARINCHILD(66)  00555282  index, value; +0x0c; shares SETVARINPARENT's tail at 0x5554ca.
    GETVARINCHILD(67)  00555349  DEST FIRST, tag-tested before the id is even read; tail 0x5555ad.
    SETVARINPARENT(68) 00555402  as SETVARINCHILD with +0x10. ZERO shipped uses.
    GETVARINPARENT(69) 005554f2  as GETVARINCHILD with +0x10. Unlocks 7 scripts on its own.
    FINDSCRIPTRAND(90) 0055601a  string; counts registry matches on NAME, draws 1 + (rand mod count),
                                 answers that script's +0x8. FAILURE LEAVES THE DESTINATION ALONE.
    GETREMOTEVAR(91)   0055617f  DEST, SCRIPT ID, VARIABLE ID. Zeroes +0x48 first; EVERY failure path
                                 falls into 0x556472, which still WRITES THAT NOUGHT to the destination.
    SETREMOTEVAR(92)   00556216  script id, index, value. Checks BOTH ends of the index.

**The frame's linkage:** `+0x8` the script's own id, `+0x0c` its one `SPAWNCHILD` child's id, `+0x10` its parent's id, `+0x14` its one `SPAWNSOUND` script's id, `+0x1c` the variables, `+0x8c` how many there are, `+0x34` the string blob, `+0x38` the directory it was loaded from, `+0x74` the offset `NAME` stored. The registry is a doubly-linked list at `DAT_008791b0`, newest at the head; `FUN_0055a070` is the find-by-id walk, inlined at most call sites.

**`FUN_005587f0` RETURNS AN ID, NOT A POINTER** — `puVar7[2] = DAT_008791a8++` and that value is returned. The decompiler makes it look like a `char*` because the same stack slot earlier holds the path buffer. **The counter starts at 1 and is only ever incremented**, so 0 is a reserved failure id and **an id is never reused** — a dead id can only fail to match, never resolve to somebody else. Ids are sequential in load order. The loader also sets `puVar7[0x10] = stackSize - 1`.

**`+0x74` is seeded to -1** (`puVar7[0x1d] = 0xffffffff`) and `FINDSCRIPTRAND` skips a negative, which is what makes "has never run `NAME`" different from "is called whatever is at offset 0"; **31 of the 308 never name themselves**. **`+0x38` keeps its trailing backslash** and the concatenation is a plain `strcat`, so a reimplementation using `Path.Combine` doubles the separator.

**Teardown (`FUN_00559060`) is exactly one level deep**: it resolves `+0x14` first, then `+0x0c`, and calls the **flat** destructor `FUN_00558500` on each — which never reads their own links — then clears its parent's `+0x0c`. **So a grandchild is never killed** and survives holding a parent id that names nobody. Dormant in shipped content: none of the 48 files the corpus spawns spawns anything itself. The same teardown is what the tick loop calls on a script whose PC has gone negative.

**`SPAWNSOUND` is not dead storage.** `FUN_0055a3e0` takes a script id and a variable index, follows that script's `+0x14`, and answers one of the spawned script's variables — **29 callers, none in the interpreter**. That is the whole point of the opcode, and why all 28 uses load `EventMap.rse`.

**The eleven names spawned never match their file's case** — scripts ask for `Effects.rse`, `clock.rse`, `worn.rse`, `anims.rse` where the archives hold `effects.RSE`, `Clock.RSE`, `Worn.RSE`, `Anims.RSE` — **so resolution must be case-insensitive or every spawn fails.** All four scripts that spawn inside a loop run `REMOVECHILD` first.

**Corpus:** `SPAWNCHILD` 20/16, `SPAWNSOUND` 28/28, `REMOVECHILD` 4/4, `SETVARINCHILD` 7/3, `GETVARINCHILD` 9/2, `GETVARINPARENT` 10/6, `SETVARINPARENT` **0**, `GETREMOTEVAR` 2 (both in `zob.RSE`, both with a literal destination, so both are test-and-branch), `SETREMOTEVAR` 10/5, `FINDSCRIPTRAND` 5/2.

### What selling a thing does to its script

Decoded 2026-09-23 as disassembly, every claim re-derived by a refuter.

**The object destructor takes the script down with a mode.** `FUN_004dd0a0` (`0x004dd29b`..`0x004dd2c9`) calls `FUN_00559060( [obj+0x24], mode )` with:

| Mode | When | Evidence |
|---|---|---|
| **0** | the whole park is being emptied: `[[0x80239c]+0x1da738]` (the world state) is 2, which only `FUN_00515dd0` (`0x515dd7`, `0x515e17`) and `FUN_00515fb0` (`0x515fba`) write as a constant, each deleting every thing and then writing 1. The field is also saved and loaded (`0x5172ee`, `0x517fcd`), so a save could carry another value | three immediate writers |
| **4** | a loaded save's layout is being replayed: `DAT_00785a2c`, field `+0x24` of the action recorder at `0x785a08`, set to 1 only at `0x0040414e` by `FUN_00404140` ("Starting layout replay"), cleared at `0x4034fb`, `0x404478`, `0x404550` | disassembly |
| **7** | everything else — every sell and every move pickup in play, through the demolisher's call at `0x00528590`. `FUN_0050b780` also reaches the destructor from `0x4e850e` and `0x516c65`; whether either deletes a placed thing in play is untraced | disassembly |

`REMOVECHILD` (`0x55526a`) and the tick loop's negative-PC path (`0x551ac6`) pass **0**. The RSSE shutdown `FUN_005584b0` and the save-state reader `FUN_005597a0` call the flat destructor directly with 0.

**The mode is a bitmask, and only two bits are read.** It is passed unchanged to all four `FUN_00558500` calls.
- **`0x2`** (`TEST BL,2` at `0x00559102`), for the script named in the call only, and only when its thing word `+0xac` is set: the item's `Info.DestroyParticleEffect` (descriptor `+0x64`) is spawned by `Particles_Spawn` (`FUN_00521e60`) at the centre of the model's box at its base height (`FUN_00466b70`, the footprint cells ×10), and its emitter's area is set to the box by `FUN_00520030` — emitter `+0x44/+0x48/+0x4c`, which is the template's `Area`. With no model it spawns at the origin (`0x00559170`). The value is the item's own where it declares one and its category's otherwise (FileFormats `sam.md`, "The particle effects an item gives off" - on the clone's `docs/item-footprints` branch until it merges); 0 spawns nothing (`0x005590f7`), and every value the jungle's items use, 75 to 78, is a world effect (`OnScreen` 0 in `Tp2.plb`).
- **`0x4`** (`0x00558582`, `0x005585f8`, inside `FUN_00558500`): the heads `ADDHEAD` hung on the model are taken off it (`FUN_0044b220` → `FUN_0044b4c0`, which frees the head's render object), and so are those of walk slots in action 4, state 2. The peep is untouched.
- Bit `0x1`, and anything above `0x4`, is read nowhere.

**Whatever the mode**, inside the `+0xac` block: a sound-engine zone at `+0xd4` is released by `FUN_0051c6b0` and cleared. The loader makes it at `0x00558fda`, only when the item's `Info.CreateParticleEffect` (`+0x60`) is non-zero; the save-state reader makes one too (`0x00559f0a`). What the zone does to the sound is not decoded.

**`FUN_00558500`, the flat destructor, releases in this order** (`0x00558500`..`0x005587cd`): the music dip (`+0xb9` → `FUN_0051e710(0)`); the TOUR ride record when `+0xc8` is set (`FUN_0055d3d0(+0x9c)`, whose head detaches ignore the mode, and which kills each car's particle, frees its model and stops its sound); the repair particle (`+0xcc`, `FUN_0051ff70(h,-2)`, cleared); the scream (`+0xd0`, `Sound_StopFading` with fade `0x3c`, **not** cleared); the ADDHEAD array `+0x30` and the walk slots `+0x2c` as above; the live count `DAT_008791ac`; the registry link; the body, variables, stack, limbo slots, bounce slots, walk slots, head array, strings and directory; and last the `+0xb0` effect list — particle types 1-2 by `FUN_0051ff70(h,-2)`, sound types 3-10 by a plain `Sound_Stop` (not a fade), `DAT_008791b4` decremented per node. **It releases no peep**: limbo, bounce and walk storage is only freed. The destructor's "object removed" message has already let everybody go — see `park-engine.md`, "Selling and the people on it".

**Two writes through NULL**: where the parent id names nobody, `MOV [EAX+0xc],EAX` with `EAX` = 0 at `0x005592f3`, and on the empty-registry arm at `0x00559316`. **`FUN_005da3c0` is a single `RET`**, so every log and assert string this family passes it is inert.

**OpenTPW** takes a sold thing's script down through `ParkRides.Unbind`, the scheduler's `Destroy` with mode 7's meaning: the particle is counted (`DESTROY_PARTICLE_EFFECT`), and `0x4` has nothing to undo because nothing hangs a head.

### `COAST` — the script-to-ride interface, and it is small

Handler `0x00554a5a`: it fetches the selector **raw** (no value resolution), does `DEC` / `CMP 7` / `JA`, and jumps through an **8-entry table at `0x556a5c`**:

    op 1 ADDPEEP      0x00554ac7      op 5 SETCLOSED    0x00554bab
    op 2 GETQUEUE     0x00554af5      op 6 SETCAPACITY  0x00554bd9
    op 3 GETPEEP      0x00554b39      op 7 SETWORN      0x00554c07
    op 4 SETBROKE     0x00554b7d      op 8 INITIALISE   0x00554a88

**Because the selector is raw, a variable selector would fall out of 1..8** — and all **144** shipped uses are literals, so that never happens. `ScriptDefs.Coaster`'s names match the table exactly.

**The ops split into get and set.** Ops 2 and 3 fetch their operand raw, call the ride, put the answer in the **result register** (`+0x48`), and only then write the variable if the operand is one. Ops 1/4/5/6/7 resolve their operand to a value first and pass it in. Corpus argument kinds agree: `ADDPEEP`/`GETPEEP`/`SETCAPACITY`/`SETWORN` take variables, `GETQUEUE`/`SETBROKE`/`INITIALISE` literals, `SETCLOSED` both.

**The complete map — every op read, none inferred from its name:**

    op 1 ADDPEEP      resolve arg -> FUN_0043b0e0( ride, value )
    op 2 GETQUEUE     FUN_0043b1f0( ride ) -> result register, then variable if the operand is one
    op 3 GETPEEP      FUN_0043b220( ride ) -> result register, then variable if the operand is one
    op 4 SETBROKE     resolve arg -> FUN_0043b270( ride, value ), which takes 0, 1 AND 2 - not a boolean
    op 5 SETCLOSED    resolve arg -> FUN_0043b2f0( ride, value )
    op 6 SETCAPACITY  resolve arg -> FUN_0043b330( ride, value )
    op 7 SETWORN      resolve arg -> ***NOTHING***
    op 8 INITIALISE   FUN_0043b050( script id ) -> ride handle at +0xe0, then FUN_0043b2b0( ride, speed )

**`COAST_SETWORN` is a no-op in the shipped engine.** `0x00554c07` fetches its operand, resolves it through `FUN_005573a0`, cleans the stack and **returns without calling anything** — the value is thrown away. Twelve shipped scripts use it. **This is exactly why the handler must be read rather than the name trusted**: implementing `SETWORN` from `ScriptDefs.Coaster` would invent a wear system the original does not have, and it would look entirely plausible. Wear is maintained elsewhere, or not at all.

**`INITIALISE` binds the script to its ride.** `FUN_0043b050` walks a linked list from `DAT_00790fe0`, matching `node[+0x24]` against the **script's own id** (field 2, the same id the scheduler uses for `(id ^ tick) & 7`), and returns `node[+0x14]`. That handle is kept at **`+0xe0`** and every other op passes it. **A null handle makes the op a no-op.** It then pushes the script's speed word into the ride via `FUN_0043b2b0`, the same call the execution loop makes each turn.

**`COAST 2 0` is idiomatic, not a bug — and it proves the whole model.** `Coaster1` word 44 is `COAST 2 0` followed at 47 by `BRANCH_Z`. The literal `0` is a **deliberate dummy destination**: the queue length lands in the result register, the store is silently skipped because the destination is not a variable, and the branch tests the register. So the "ignored write" path is a **feature the scripts rely on**, which is why an interpreter must skip rather than throw.

**Only 12 scripts use `COAST` at all**, and every op count is a multiple of 12 — the coaster family is near-identical across the four themes.

### `BUMP` and `TOUR` — a different object family

Dispatch at `0x005546f5`: `DEC EAX` / `CMP EAX,0x10` / `JA 0x00554c25` / `JMP [EAX*4 + 0x556a18]` — **17 entries, selectors 1..17**, table immediately BEFORE COAST's at `0x556a5c` (over-reading BUMP's table by three entries returns COAST's `00554ac7`/`00554af5`/`00554b39`, a free corroboration of the COAST map).

    sel  1 00554726   sel  5 00554836   sel  9 00554895   sel 13 0055495e
    sel  2 00554766   sel  6 005547f6   sel 10 005548da   sel 14 00554996
    sel  3 00554816   sel  7 005547d6   sel 11 005548fa   sel 15 *** ERROR HANDLER ***
    sel  4 005547b2   sel  8 00554850   sel 12 0055493b   sel 16 005549c8 / sel 17 005549ed

**Selector 15 is invalid in the binary** — its table entry is the error logger itself — **and the corpus confirms it: 199 uses across 12 scripts, selectors 1..14/16/17, and ZERO uses of 15.** A falsifiable prediction from the binary that the data upheld.

**The prologue is the same as COAST's; what differs is the object the HANDLERS use, not how the dispatcher starts.** Both begin with the identical three instructions (`MOV EAX,[0x007cf6ec]` / `MOV DX,[EBP+0xac]` / `CALL 0x004cd300`). The prologue reads a 16-bit field at `[EBP+0xac]`, looks an object up through the global at `0x007cf6ec`, and every BUMP handler passes **`[ESI+0x28]`** into functions in `0x00544xxx`-`0x0054axxx`. COAST instead uses the `+0xe0` handle INITIALISE stores, indexing `DAT_00790bd0`, with functions in `0x0043bxxx`. **Two unrelated object families** — so one "ride" serving both would be an abstraction the original does not have.

Sel 5 reads `[ESI+0x2c]` directly with no call; sel 13 and 14 call the **same** function `0x00545100`, one scaling its argument by **30** (frames per second) and the other negating it; sel 7 is the only handler with no observable effect besides its call. **EDI is zero throughout the dispatcher**, so sel 4 passes a literal 0 and sels 8, 9 and 17 are "if the resolved value is non-zero" guards.

**COAST and BUMP share one error handler; TOUR does not.** COAST's `JA` (after `CMP EAX,0x7`) and BUMP's `JA` (after `CMP EAX,0x10`) both jump to **`0x00554c25`**, which pushes `0x765bac` = **"RSSE: Unknown bumper ride command"** — so an out-of-range COAST command is reported as a *bumper* fault, logged through `FUN_005da3c0`. TOUR has its own at **`0x005546dc`** pushing `0x765bd0` = "RSSE: Unknown tour ride command", and TOUR's own dispatch is `DEC` / `CMP EAX,0x11` / `JMP [EAX*4 + 0x5569d0]`, so **18 selectors**. **Never write an engine string from memory or by analogy** — one was invented and caught only by reading `0x765bac`.

**`ScriptDefs.Bumper` is NOT a selector table — do not implement from it.** Its values are -1, 0, 7, 32, 38, 47, 54, 93, 115, 121, 134 and **18770**, and the dispatcher accepts only 1..17, so they cannot be selectors at all. `ScriptDefs.Coaster` matched COAST's table exactly, which makes the mismatch here easy to miss by analogy. Whatever those numbers are, they are not what `BUMP` switches on.

### The ride object

**The ride object is twelve functions around `0x0043b050`**: b050, b080, b0c0, b0e0, b130, b1f0, b220, b270, b2b0, b2f0, b330, b390. COAST reaches **eight** of them; b080, b0c0, b130 and b390 are reached from elsewhere (`FUN_0043b2b0` is also called by the VM loop `FUN_005516b0` and by `FUN_00447e30`), so they are the ride's own behaviour rather than the script's view of it.

Every one of those functions opens with `ride = *(int *)(&DAT_00790bd0 + handle * 4)`, so **the handle `INITIALISE` keeps at `+0xe0` is an INDEX into a table**, not a pointer, and `FUN_0043b050` returns `node[+0x14]` as that index.

    GETQUEUE   0043b1f0   max( 0, ride[0xb8] - ride[0xc0] - ride[0xbc] )
    ADDPEEP    0043b0e0   if ride[0xc0] + ride[0xbc] < ride[0xb0]:
                            *ride[0xc8] = value; advance modulo ride[0xb0] from base ride[0xb4];
                            ride[0xbc]++
    GETPEEP    0043b220   if ride[0xdc] != 0 and ride[0xe4] != ride[0xec]:
                            value = *ride[0xe4]; advance modulo ride[0xd0] from base ride[0xd4];
                            ride[0xdc]--; return value      else return 0
    SETCLOSED  0043b2f0   arg 0: if ride[8] & 0x0A -> FUN_00435730( ride, 4 )
                          else : if ride[8] & 0x04 -> FUN_00435730( ride, 8 )
    SETCAPACITY 0043b330  clamp >= 0, then to [ride[4]+0x318], then to [[ride[0x128]+4]+8];
                          if it differs from ride[0xf0] -> FUN_0043ba90( ride, value, 0, 1 )

**SETCAPACITY and GETQUEUE are one number, and only `FUN_0043ba90` says so.** b330 clamps and hands the value to ba90 **without touching `ride[0xb8]`**, which is what GETQUEUE measures against — so reading b330 alone says capacity and queue room are separate quantities, and a ride object built on that would diverge from the original on the very sequence `Coaster1` runs (SETCAPACITY at word 22, GETQUEUE at 44). **ba90's last line is `ride[0xb8] = ride[0xf0]`**, so they are the same number and one Capacity field is correct. ba90 also sets `ride[0xf0]` itself, from its second argument, when its fourth argument has bit 1 — which SETCAPACITY always passes.

**Three things here would be got wrong by guessing:**

- **`GETQUEUE` returns ROOM REMAINING, not queue length.** That inverts the sense of `Coaster1`'s `COAST 2 0` / `BRANCH_Z ->62`: it branches away when there is **no room**, not when the queue is empty. The `((int)x < 0) - 1 & x` idiom on the end is a **clamp to zero**, not a sign test.
- **`SETCLOSED` is a STATE-MACHINE TRANSITION**, not a boolean — it only acts when the current state bits at `ride[8]` allow it, and requests the change through `FUN_00435730`.
- **`SETCAPACITY` is clamped by two independent limits** before it is applied.

**Two ring buffers, not one:** the queue at base `0xb4` / capacity `0xb0` / cursor `0xc8` / count `0xbc`, and a second at base `0xd4` / capacity `0xd0` / head `0xe4` / limit `0xec` / count `0xdc`. `ADDPEEP` fills the first; `GETPEEP` drains the second.

### Corpus shape, and what a world-less interpreter can reach

**84 of the 106 opcodes are used**; scripts end in `BRANCH` (294) or `RETURN` (14) and **never** in `END`, so they are endless loops by construction; **277 of 308** open with `NAME`, which is a tendency and not an invariant.

Corpus counts for the control opcodes: `WAIT` 458, `ENDSLICE` 396, `CRIT_UNLOCK` 240, `WAIT4ANIM` 170, `CRIT_LOCK` 150, `JSR` 70 (all targeting labels), `HUSH`/`HOP` 39 each (always a variable), `RETURN` 33, `TURBO` 20, `NOP` 2, and **`END` zero times**.

**The critical sections, walked as control flow** (every branch arm taken as possible, `JSR`/`RETURN` followed, each section walked to its `CRIT_UNLOCK`, a yield or `END`; measured twice, by the project's reader and by an independent one): the 150 `CRIT_LOCK`s sit in 128 scripts, 36 of them in 81 Lost Kingdom scripts. Every one is reached at call depth 0 and none while already locked. **No section contains a loop.** Nine branch backward inside the section, and none of those edges closes a cycle: five to a test after the lock (the lane choice in `Junspray`, `Hyenas`, `Squirtem`, `frushy` and `Marsmoon`), and four to a `CRIT_UNLOCK` placed before it (`TourRide` @142 in Lost Kingdom and Hallowe'en, `twetours` @169, `scitour` @176). **The longest runs 23 instructions** after its lock, the unlock included (Hallowe'en `GoKarts.RSE` @69); in Lost Kingdom 19 (`GoKarts.RSE` @52), then 17 (`SupBog` @46, `Wateride` @77). **Seven sections can yield while still locked**, so the rest of each runs unlocked on the next turn: the four lane sideshows, `Junspray` @22 and `Hyenas` @22 in Lost Kingdom and `Squirtem` @22 and `frushy` @20, each with a `WAIT` after a `WALKON`; `Purse` @22 (`WAITANIM`); and the Hallowe'en and space `bumper` (@86, @95).

**A world-less interpreter cannot reach most `WAIT`s, and that is correct.** `Coaster1.RSE` disassembles to 122 words with **exactly one `WAIT`, at word 88**, and it sits behind `TEST $VAR_BREAKSTAT` / `BRANCH_NZ` at word 63. **Nothing in the script ever writes `VAR_BREAKSTAT`** — only a running park does — so with no world it stays 0 and the script loops `18 -> 67 -> 18` forever, never reaching 88. A test asserting "it reaches a WAIT" fails against a perfectly correct machine.

**And that main loop contains no `ENDSLICE`.** The only reason a turn ever ends is the `CRIT_UNLOCK` at word 62 zeroing the budget — so a machine that misses that semantic spins forever inside one turn rather than failing visibly. `Coaster1` also declares **no stack at all** (`#setstack` 0), so it exercises nothing of `JSR`/`RETURN`. **The useful shape for a test is to *find* a script that reaches a `WAIT` by running them all, not to name one.**

**A lock reached on the last unit of the budget**, the one arrival where charging `CRIT_LOCK` would end the turn inside its section. Walked with every branch arm possible from every turn start (the entry, after each `ENDSLICE` and `CRIT_UNLOCK`, each waiting instruction, and wherever a budget of 50 runs out, to a fixpoint), **68 of the 150 locks can be dispatched after exactly 49 costed instructions, 18 of the 36 in Lost Kingdom**; three walkers written apart agree lock by lock, and all 68 witnesses replay from word 0 on a separate machine. **None of the six locked scripts the Easymode park places is among them** (the three toilets, `Coconut`, `Bouncy`, `Junspray`: their locks come after at most 29). **But nothing else runs during a script's turn**, so a world answer (`LETMEOFF`, `RIDECLOSED`, `LIMBOSPACE`, `WALKGET`, the clock, a `WAIT4ANIM` deadline) is the same every time one turn asks it. Walked that way, 15 of the 18 cannot happen: a poll loop exits only on its first test in a later turn, and the arrival is the phase plus a fixed path, well short of 49. The three left (`bumper` @92, `GoKarts` @52, `Wateride` @77) survive only because `BUMP`'s answers are undecoded and taken as free to change within a turn. **The Hot Pot's `bumper` @92 is the only one the project's interpreter can reach**, because its `BUMP`s are no-ops there: from the turn that resumes at its `WAIT 1000` @134, the ride unloads in one run of 18 instructions a rider, removes cars at 9 each and reaches the lock after `(19 + 18n + 9d) mod 50` costed instructions of its turn, for `n` riders and `d` cars removed. 49 needs 8 riders cut to 4 cars, or 7 cut to 1: the capacity cut during the ride. Its capacity runs 1 to 8 (`MinCapacity`, `MaxCapacity`), red-lined at 4.

---

## Arrivals: who comes, on what, and how often

`FUN_004cf3e0` is the arrival manager. Its state is two fields of a small block: `+0x10` is "a load is
being dropped off" and `+0x0c` is how many people are left to drop.

| | |
|---|---|
| `FUN_004cf3e0` | the manager, its block at world `+0x2c4` |
| `FUN_004d7b20` | its one caller, once a thing sweep (`0x004d7b29`, through the thunk `0x004cf3d0`) |
| `FUN_004cf5b0` | how many people this load carries |
| `FUN_0051a2f0` | picks, and if necessary **creates**, the vehicle |
| `FUN_004cf720` | makes **one** guest |
| `FUN_0041a990` / `FUN_0041a960` | reads and resets the arrival timer |
| `FUN_004cf030` / `FUN_004cf050` | builds the block / reads and writes it in a save |
| `FUN_004c7fa0` | the cached count of guests already in the park |

**The cycle.** While no load is in progress it compares `FUN_0041a990()` against `DAT_00785314`. When
that passes it asks `FUN_004cf5b0` for a headcount, logs `"Bus about to arrive with %d people"`, sets
the offloading flag, and pushes the count into a ring buffer on the analyser thing (`+0x216dc`, cursor
`+0x216f0`, capacity `+0x216f4`, wrapped flag `+0x216f8`). While the vehicle reports state **2** it
logs `"Bus: dropping off kids"` and calls `FUN_004cf720` **once per call**, a thing sweep, decrementing the count
and bumping a running total at `+0x20cc0`. The first call that finds the count at nought or below resets the timer,
clears the flag, and sends the vehicle away. The logging call is a bare `RET` (`0x005da3c0`), so none of these lines
is ever printed. The state-6 arm (`0x004cf475`) is dead by CODE: `FUN_0051a690` never returns 6.

**The clock is `mGameTick`, one count per thing sweep, so a load is called about every 149 s.** The manager has
one caller, and it runs once a sweep: `FUN_00516380` increments `mGameTick` (`0x00516394`) and then, on every path
through it, calls `FUN_004d7b20` (`0x00516695`), which calls the manager's thunk with the block at world `+0x2c4`
(`0x004d7b29`). The sweep runs on the 31 ms steps whose counter has its low three bits nought (`0x0054f668`), every
248 ms (`park-engine.md`, "What the 31 ms tick drives"). `FUN_0041a990` is `(mGameTick >> 2) - (mark >> 2)`, the mark
at block `+4` (`0x004cf3ee`), and it reads `mGameTick` through `[0x0080239c]`, which `FUN_00515660` points at the
world `FUN_00407d80` allocates and stores at `[0x007cf83c]`, the counter the sweep increments. **The compare is
unsigned and strict** (`CMP EAX,[0x00785314]` / `JBE`, `0x004cf3f6`): a load is called on the first sweep whose
elapsed count is more than the period, 151 for 150, which is `604 - (mark mod 4)` sweeps after the mark, 601 to
604, 149.0 to 149.8 s. One count of `mGameTick >> 2`, four sweeps or 0.99 s, is the second the designers wrote in:
the advisor's per-message gate runs on the same helper (`FUN_0059abc0`, `0x0059ac64`), and `Advisor.sam` comments its
`MinTimeSameMessage` 1800 as "Half an hour", which is 29.8 minutes counted in fours of sweeps and 3.7 counted in fours
of 31 ms ticks. The mark is reset (`FUN_0041a960`, `0x004cf59c`) by the first call that finds the vehicle at
state 2 with nobody left (`+0xc <= 0`, signed, `0x004cf56b`). The call that drops the last guest goes to the tail
instead (`0x004cf594`), so the reset comes a sweep after that drop at the soonest, and **the next load is called 602
to 605 sweeps after the last guest got off, 149.3 to 150.0 s**. The vehicle's run in and the drip come on top. A mark
ahead of the clock makes the difference wrap, and a load is called at once.

A sweep the loop cannot run is dropped, not owed (`park-engine.md`, "What the 31 ms tick drives"), and this timer
falls behind the wall clock with it.

**The mark is saved, so a park entered from its save carries on the wait it was saved in.** Each park entered gets
a new world (`FUN_00407d80`, `0x0054eccb`), whose constructor builds this block with `FUN_004cf030`: mark 0, `+8` 5,
`+0x11` 1. `FUN_004cf050` reads and writes it as `mArrivalRate` (`+0`), `mTimeSig` (`+4`, through `FUN_0041a860`,
`0x004cf296` in the read arm), `mTargetVehicleCapacity` (`+8`), `mPeopleOnBus` (`+0xc`), `mOffloading` (`+0x10`) and
`mGatesOpen` (`+0x11`): the last 18 bytes of the World block's 76 bytes of arrival and clock fields (FileFormats,
`saves.md`). Entering a park zeroes `mGameTick` (`FUN_005156a0`, `0x00515865`, from `FUN_00407e00` at `0x0054ed3f`).
Later in the same pass of state 9, `FUN_005accf0` (`0x0054f12b`) loads the newest `*.TPW*` in the player's folder for
the theme over it, through `FUN_00414d40( path, 0, 2 )` (`0x005ad054`), `FUN_00415270` and `FUN_005179c0`, which reads
`mGameTick` at `0x00517bec` and this block at `0x00518202`. Nothing writes either between that load and the first
sweep. On an Instant Action player's first entry the file is the copied `Easymode.TPWI`, which holds `mGameTick` 755
and `mTimeSig` 661, so **Lost Kingdom's first load is called on the 509th sweep after entering, 126.2 s in** (1264 is
the first count with `(n >> 2) - 165 > 150`).

**Which vehicle comes is decided by how many people are coming, not at random.** `FUN_004cf3e0`
computes `1` for a headcount under `0x24` (36), otherwise `(0x3c < count) + 2` — so `2` for 36 to 60
and `3` beyond. That value is `FUN_0051a2f0`'s third argument, where **1, 2 and 3 force the bus, the
seaplane and the ferry** and **0 means choose at random**. The random arm is an LCG —
`x = x * 0x19660d + 0x3c6ef35f`, rotated right thirteen, made positive, `% 3` — run over
**`mRandomSeed`** (`+0x1da708`), the save's own seed. The spent load and the tail pass 0 (`0x004cf4f2`, `0x004cf50c`,
`0x004cf526`, `0x004cf54a`), but a current vehicle is reused whatever the argument, so the random arm is reached only
from the tail's call when no vehicle is current (`0x004cf526`).

The save agrees from the other side: its header fields are named `mArrivalVehicle_Size1`, `_Size2`,
`_Size3` and `mCurrentArrivalVehicle` (`FUN_00516c80`), and `FUN_0051a2f0` caches the three at
`+0x1da72c`, `+0x1da72e`, `+0x1da730` with the current one at `+0x1da72a`.

**The vehicle thing is made on demand.** Where the slot is empty, `FUN_0051a2f0` looks the feature up
by name, allocates `0x450` bytes, calls `FUN_004db090( 1, <name>, 0, 0 )`, takes the id from
`FUN_0050b350` and caches it. If the pick is unavailable it falls back through the other two by
bitmask, and if all three fail it dies with `"Fatal error: Could not find either the bus or the plane
feature"`. A missing feature gives `"Could not find the 'vehicle_name was here' feature"` — the
placeholder is the shipped string.

**This is why Lost Kingdom places a bus and neither a ferry nor a seaplane.** Its `_Size1` slot holds
the bus and the other two are nought, which says the park has only ever had small crowds arrive.

**How many come.** `FUN_004cf5b0` returns **0 outright when `mWorldState` (`+0x1da738`) is 4**. Otherwise the count is
`max( MinPeople, ftol( (NewParkBonus + score) * k ) / max( PointsPerVisitor, 1 ) )` (`0x004cf5dd`-`0x004cf648`), the
score `FUN_004c8240` and `k` 0.8 when `[FUN_00519590() + 0x30]` is above nought, 1.2 otherwise (`0x00700368`,
`0x00700364`). `NewParkBonus` is added on every call; nothing here asks whether the park is new. It is then capped
against the population `FUN_004c7fa0` reports: **500 in the online mode** (`DAT_00fb3b7c == 1`) and **1500 (`0x5dc`)
otherwise**, each logging `"Capping the number of people in o..."`. It then logs `"Number of people is %d"`.

**A guest is made at a cell, not carried in the vehicle.** `FUN_004cf720` asks `FUN_004d8650` for the second bus
stop (argument 1, pushed at `0x004cf745` before either arm) and, when `FUN_0051aad0` reports a vehicle standing,
subtracts `0x100` from the packed id (`0x004cf75c`). `FUN_004d8650` packs `y * 128 + x + 1` (`SHL EAX,0x7`,
`0x004d8663`), so that is two rows: Lost Kingdom's (53,5) becomes (53,3). It then allocates `0x22c` bytes and
constructs the person there. **So the vehicles are mechanism rather than transport**: nobody is ever inside one.

**Which balance keys these globals are, proven.** Nothing writes them by name: the balance loader stores each
value at a slot its descriptor's place in the table gives it (`park-engine.md`, "How a key finds its global"; the
`PeepInfo` object at `0x00785040`). Walked over all 283 descriptors from the file on disk, the table closes exactly on
the next object (`0x00785828`), and `Arrival.MinPeople` (descriptor 93) lands on `0x00785310`, the floor;
`TimeBetweenArrivals` on `0x00785314`, the period; `FixedRate` on `0x00785318`, which nothing reads (no reference, no
bytes `18 53 78 00`); `NewParkBonus` on `0x0078531c`; and `PointsPerVisitor` on `0x00785320`, the divisor. And
`FixedItemInfo.BusStopAPosX`/`Y` land on `0x007855ac`/`0x007855b0` and `BusStopBPosX`/`Y` on `0x007855b4`/`0x007855b8`,
which is what `FUN_004d8650` reads for arguments 0 and 1; `FUN_004d8690` reads `EntranceA`/`B` the same way. Three were
known by their readers before the walk (the floor, the divisor, and the stop ten arrays further on), and all three land
where they should.

`+0x1da720`, the ushort thing id `FUN_00519510` reads to reach the analyser's counters, is `mParkAnalyser`: the
header's writer `FUN_00516c80` pushes the string `mParkAnalyser` at `0x00516f8c` and pairs it with
`LEA ECX,[EDI+0x1da720]` at `0x00516fa0`.

### What the balance file supplies, and the one score that is not decoded

The global `Standard.sam`; jungle's own `Standard.sam` overrides none of these, and its `Easy_Standard.sam`, read
over both for an Instant Action park, sets `PointsPerVisitor` to 5:

    Arrival.MinPeople            1
    Arrival.TimeBetweenArrivals  150
    Arrival.FixedRate            15
    Arrival.NewParkBonus         20
    Arrival.PointsPerVisitor     6

Each fills the global named above (proven by the slot table). Read them with `ParkBalance.Int( "Arrival.X",
fallback )`: the `SAMParser` quirk applies only to multi-value lines, and these five are ordinary single-value keys.

**OpenTPW keeps the same clock and the same mark, and takes the manager's arms in its order** (`ParkPeople.StepArrivals`,
Q68b). `ParkState.GameTick` is `mGameTick`: seeded from the save and one up as each thing sweep begins, before
anything in it runs. The mark starts from the save's `mTimeSig` (`ParkWorld.Arrival`). A load is called when
`ParkPeople.LoadIsDue` finds the fours of the clock more than the period past the mark's, unsigned. The call goes
straight on to ask the vehicle, a guest is dropped a sweep while it answers 2, and the load is let go, the mark reset
and the vehicle sent away on the first sweep after the last drop that still finds it at 2; `StepVehicle` does not
release 2 while a load is held. Measured in the running game (`q68bmeasure.py`, jungle, silent), each predicted
first: the first load called on `mGameTick` **1264**, 509 sweeps after 755 and 126.05 s after the park came on show
(126.23 predicted); its guest dropped on 1300, the bus having driven in; the load let go on **1301**; the next called
on **1904**, 604 sweeps after the drop and 149.54 s after the let-go; that one dropped on 1904 and let go on 1905.

What it does not reproduce, each said at its site: the headcount, the floor alone (Q26); where each guest is made,
stop A and stop B in turn (Q127); the two refusals, in world state 4 and at the cap, where the original calls a load
of nobody or of what fits and still sends its vehicle (neither reached in Lost Kingdom); a load saved half-dropped,
counted as `SAVED_ARRIVAL_LOAD` and not resumed; guests made with no script to ask, on the sweep that calls the load;
and the spent vehicle, which is sent round again and waits at the stop (Q131), which is why the second load above
dropped on the sweep that called it where the original's bus would have driven in first.

**The score in the headcount is `FUN_004c8240`, and it is NOT decoded**, nor is `FUN_00519590`'s `+0x30`. It sums a
park-attractiveness score over the rides — per ride a capacity, a duration divided down, and a
three-entry table at `+0x268`. It reads four ride fields this project has not named. **OpenTPW reproduces the
floor alone**, which is a declared deviation with a visible consequence: `MinPeople` is 1 in every theme the game
ships, where even a score of nought brings `ftol( 20 * 1.2 ) / 5` = 4 to an Instant Action Lost Kingdom (3 at 0.8), and
the vehicle is chosen by crowd size, so a park left to itself **never** selects the seaplane or the ferry. Deciding
the real headcount is what would change that (`docs/QUEUE.md` Q26).

**What a new guest's fields come from**, so nothing here is invented: `Cash` is
`PeepTypes[x].StartingCash` varied by `PeepInfo.StartingCashVarPc` (15, per cent); `ExitLevel` is
`PeepInfo.ExitLevel` (120), which the file itself calls *"starting value for the ExitLevel counter, in
SECONDS"*, varied by `ExitLevelVar` (60). It counts down one per needs tick — `DueOn` is
`(ThingId & 3) == (tick & 3)` on **thing** ticks, so roughly one a second — with no clamp. The state-6 turn
sends a guest home when it reads **exactly** nought, so only a guest deciding within those four sweeps leaves
for it (`ride-operation.md`, "The state-6 turn, in order", arm (d)).

## The save's world block: map cells

Derived from an emulator field log (`fields_005179c0.txt`, i.e. the fields of `FUN_005179c0`) **which names every field of every one of the 16,384 cells**. Both record sizes match `ParkWorld`'s independently measured skip **to the byte**.

    save_status_byte  1
    bit 1 -> MAP record, 52 bytes
        mDirection 1 | mFlags 2 | mMeshInstance 4 | mNeighbours 1 | mOverlapCounter 2 |
        mParentID 2 | mTileData 12 | mType 4 | mHoardingNeighbours 1          <- 29, the tile base
        mLitter 4 | mLitterCollector 2 | mLitterScript 4 | mLitterScript 4 |
        mPylonIndex 2 | mStatusFlags 1 | mTimeMarkedForLitterCollection 4 | mWho 2    <- 23
    bit 2 -> TRACK record, 31 bytes = the same 29-byte tile base + mSegmentNumber 2
    bit 4 -> effects, 10 bytes (= the RegionFX record)

Jungle: **16,134 cells are status 3 and 250 are status 7**. The first cell's status byte is at payload offset **0x1a6d**, and within a cell `mDirection` is at +1, `mFlags` +2, `mNeighbours` +8, `mType` +0x19.

**The cell sizes are confirmed cell by cell, not just in total.** Measuring all 16,384 by their status byte lands on the next cell's status byte **every single time, 0 mismatches**. That is a sharper check than the World block's own `DLRW` trailer, which a pair of compensating errors would still reach.

### `mType` over the shipped park

    7 = 9,077 | 0 = 6,875 | 2 = 240 | 1 = 78 | 30 = 66 | 4 = 35 | 9 = 8 | 3 = 4 | 10 = 1

- **mType 1 is a PATH** — its 78 cells draw a loop from x39..56, y21..28 with a double-wide avenue at x47,48 running down to the entrance.
- **mType 4 is a built object's footprint**: its cells sit exactly on the Belly Bounce's 3x4 at (51,23) and on the east cluster.
- **mType 30** = 66 cells, **exactly** the count of `base.map` cells carrying 0x80, so it is the fixed approach.
- **mType 3** (4 cells, at the ride's entrance) is a **queue cell** — see `park-engine.md`, "A queue is a re-derivable walk, not a stored link".

**mType 4, 9 and 10 are one family: a built thing's footprint.** Together they are **44 cells = exactly the eleven placed objects' footprints**, in seven connected groups whose boxes are the items' own sizes: (57,15) 3x5 = staff 2x2 at (58,15) + fountain 3x3 at (57,17); (51,23) 3x4 belly bounce; (51,30) 3x3 jungle spray; (43,29) 2x3 = litter bin + drinks shop; (55,15) 1x3 = the three toilets; and the two cameras. **9 is a thing's entrance and 10 its exit** — `FUN_00413410` tests the item's shape grid for the literals 9 and 10 (`park-engine.md`, "Where a built thing's entry and exit cells come from"); that all three belong to a footprint is measured.

**Around a ride, the types make an arrangement worth knowing.** The Belly Bounce's 3x4 box at (51,23) is mType 4 on ten of twelve cells, with **mType 9 at (52,23)** and **mType 10 at (52,26)** — the two ends of its middle column — and the **four mType-3 queue cells in the row beyond the 9**, at x49..52, y22. That reads as a way in and a way out, **and 9 marks the entrance of any thing, not only a ride**: it has 8 cells park-wide and some land on the drinks shop and the litter bin.

### `mNeighbours`, `mDirection` and the compass

**`mNeighbours` is stored, one byte a cell**, along with `mDirection` — so a renderer does not have to compute the neighbour mask, only map it to a tile. Over the 78 path cells the mask takes 32 distinct values (68 x14, 17 x11, 31 x6, 241 x6, ...), which is the spread a real tile set needs.

**`mDirection` is a single compass bit, and it corroborates the mask's bit order.** Over those same 78 cells it only ever reads **0, 1, 4, 16, 64** — bits 0, 2, 4 and 6, i.e. exactly the four cardinals. A path tile only ever faces a cardinal direction, and the two fields share one compass.

The compass is `0x01 N, 0x02 NE, 0x04 E, 0x08 SE, 0x10 S, 0x20 SW, 0x40 W, 0x80 NW` **with N at -y**, so `0x44` (E+W) is a horizontal straight at angle 90 while `0x11` (N+S) is a vertical one at angle 0. **The two edge masks are the proof of the bit order**: `0x1f` is N,NE,E,SE,S and `0xf1` is N,S,SW,W,NW — every connection on one side, which is what an edge tile is.

**ANSWERED 2026-09-21 — the rule that GENERATED `mNeighbours` is `FUN_005348d0`, and the reason no sweep could ever fit it is that it is INCREMENTAL and ORDER-DEPENDENT.** Full decode in `docs/exe/park-engine.md`, "Building and deleting paths and queues". A sweep of member sets x diagonal rules tops out at **67/78**, and splitting the member set so cardinals admit `{1,9,10}` while diagonals admit only `{1}` reaches **73/78** — but no pure function of the final map can reach 78, because:

- the cardinal test is **type-dependent**: mType 1 links unconditionally, mType 10 only when `nb.mDirection & Opposite(D)`, mType 9 only when `nb.mDirection & D`, and mType 3 **never forms a new link at all**;
- diagonals are set by **two non-equivalent tests**, one strict and symmetric on the cell being placed, one weak and **one-sided** on its neighbour — so `mNeighbours` is legitimately asymmetric;
- a final **prune loop clears the two diagonals flanking any cardinal that points at an mType 3 or 9 cell**, which is why four diagonals are withheld.

So the 11 dissenters were never dissenters: 9 of their differing bits point at mType 9, 3 and 10 cells (the type-dependent cardinal rule) and all 4 of the others are diagonals (the prune loop). **Validate any implementation by replaying creation order, never by evaluating a predicate over the finished map.**

**Rendering a saved park still does not need any of this** — the mask, the tile and the angle are all stored. It is needed to *place* new paths, so it belongs with editing, not with drawing.

### `mTileData` is three dwords — tile set, tile index, rotation in degrees

Measured over every cell:

    dword 0   tile set    1 on all 78 path cells, 2 on all 4 queue cells, 0 on the other 16,302
    dword 1   tile index  path cells use 2,3,4,5,6,7,9,10,15,19,20 - all inside PathTex 0..21
    dword 2   rotation    0, 90, 180 or 270 on every one of the 16,384 cells, never anything else

Two independent things support the split and a wrong one would have to produce both by accident: dword 0 divides the map **exactly** along the boundary `<Theme>.tct` draws between its `PathTex` and `QueueTex` sections, and dword 2 is a quarter turn everywhere. **So a path carries the tile it draws AND the turn it takes** — neither has to be inferred from neighbours.

`FUN_005365d0` confirms the layout from the engine side: it reads a cell's tile fields as **three consecutive dwords** (set +20, index +24, angle +28) and tests the set against 2.

Non-path cells carry constants rather than nothing: `(0, 8, 0)` on every mType 7, 2, 30 and 4 cell, and `(0, 55, 0)` on every mType 0 cell. Only **25 distinct `mTileData` values exist in the whole park**.

**dword 1 IS the `PathTex` row for set 1, and the tileset is an AREA set.** `Jungle.tct` read out of `terrain.wad`, then each index cross-tabulated against the shape its cell's neighbours actually make. **Every index lands on a tile whose name matches its measured topology:**

    2 jpa_str1 / 19 jpa_str2   degree 2, collinear      straight, two art variants
    3 jpa_cnr2 /  9 jpa_cnr1   degree 2, bent           corner
    4,6,7,15 jpa_tju1..4       degree 3                 T-junction
    5 jpa_xrd1                 degree 4, mask 0x55      crossroads - the park's ONE such cell
    10 jpa_edg1 / 20 jpa_edg2  degree 3, masks 0x1f/0xf1  EDGE of the double-wide avenue

**This is an AREA tileset, not a line tileset** — `jpa_edg1/2`, `jpa_ctr1` (centre) and `jpa_squ1` (isolated square) exist because a walkway can be more than one cell wide, which the entrance avenue at x47,48 is. **Do not build a renderer that assumes 1-cell-wide paths.**

### A queue index names a MODEL, not a texture row

Lost Kingdom's queue is a run of four at y22, x49..52:

    (49,22) set 2 index 5 angle 270 nb 0x44      <- where the queue MEETS the path at (48,22)
    (50,22) set 2 index 2 angle 270 nb 0x44
    (51,22) set 2 index 2 angle 270 nb 0x44
    (52,22) set 2 index 3 angle  90 nb 0x50      <- S+W, a corner

**Index 5 cannot address `QueueTex`, which has only rows 0..3 — because it was never addressing textures.** A queue is built from **seven one-cell MODELS** the theme keeps in its own `queue.wad` (a sibling of `terrain.wad`), each with a flat `base` plate and a 75-byte `.hmp` = `48 + 27 * (1*1)`. The exe holds them in **12-byte records at `0x76338c`**, walks them as it loads (`FUN_00522900`) and indexes that table by exactly this number when it places one (`FUN_005229e0`):

    0 quedead | 1 quedead | 2 questra | 3 quebnd2 | 4 quebnd1 | 5 queend | 6 quebin1 | 7 quebin2

So 5,2,2,3 = **end, straight, straight, bend** — exactly what those cells' stored masks make, 4 out of 4. `QueueTex` is real but is the ART those models are skinned with (and `queue.wad` ships six `jpa_que` textures where the `.tct` names four).

The queue models animate nothing: `quebin1m`, `quebin2m` and `queendm` are `readable False` with **zero tracks of any kind**, frames 0..0.

### Which way a saved angle turns, and about what

A footprint turns about the middle of its **ANCHOR CELL** (not the footprint's middle), and the stored angle turns the **opposite** way to a positive rotation about the engine's up axis. The save states where a thing stands twice over and independently, which is what settles it: staff room anchored (58,16)@90 is marked (58,15)-(59,16), and the fountain anchored (57,19)@90 is marked (57,17)-(59,19). Turning the other way puts the fountain on (55,19)-(57,21). Those two are the **only** rotated items with a footprint bigger than one cell, so 180 and 270 follow the rule rather than being measured.

**A queue piece turns the OPPOSITE way to a built item.** `FUN_005229e0`'s own call site places a piece at **`0x168 - angle`**, 360 minus the stored angle, folded 360 -> 0. **That is the QUEUE's rule and not the item's** — citing it as corroboration for the item rule is a misreading. Taking it the item's way put `queend`'s torches at the wrong end and turned the bend the wrong way off the ride.

    Items:  Turn(angle)          Queues: Turn(360 - angle)

**The quarter turn on paths was settled by the art rather than by data**: each walkway carries stone edging along its sides, so a correct turn runs that edging along every straight and around every corner, where a wrong one lays it across the path and through its own junctions. **That is the check to repeat, not the reasoning.**

---

# Part 3 — OpenTPW-side consequences and traps

## Loading park files: use `OpenRead`, never `FileExists`

**`BaseFileSystem.FileExists` and `DirectoryExists` are archive-blind** — they are literally `File.Exists(abs)` / `Directory.Exists(abs)`, so they return false for everything inside a `.wad`. `OpenRead`, `GetSize`, `GetFiles`, `GetDirectories` and `IsArchive` all go through `FindArchivePath` and do see inside. This is already known in the tree — `SignFile.cs` and `AnimationFile.cs` both say so in comments — so **route around it, do not "fix" it** (unrelated refactoring).

A confusing symptom to recognise: `DirectoryExists("ui")` is true while `DirectoryExists("lobby")` is false, purely because `data/ui/` exists as a real folder beside `ui.wad` whereas `lobby` is only `lobby.wad`. **Nesting depth is not the issue** — `lobby.wad` at the root and `levels/jungle/terrain.wad` two levels down behave identically.

Verified end to end through the real `BaseFileSystem` (game mounted at the real data path, `WadArchive` on `.wad` and `SdtArchive` on `.sdt`): these all open —

    levels/jungle/terrain/base.MD2      levels/jungle/terrain/base.lnd
    levels/jungle/terrain/basem.MD2     levels/jungle/terrain/base.map
    levels/jungle/terrain/Jungle.tct    levels/jungle/terrain/textures/grd_ctr1.wct
    levels/jungle/Standard.sam          levels/jungle/Easymode.TPWI

Note that `new ModelFile( path )` goes through the global `FileSystem` and throws outside the game; `new ModelFile( stream )` is the harness-friendly entry point.

## `LobbyModel` is reusable for park terrain as-is

    LobbyModel( string modelPath, string textureDirectory, Vector3 origin, float scale = 1f,
                IReadOnlyDictionary<string,Texture>? textureOverrides = null,
                MaterialFlags materialFlags = MaterialFlags.None,
                string? sharedTextureDirectory = null,
                IReadOnlyList<AnimationFile>? clips = null )

It makes one `ModelEntity` per mesh, resolves each material as `{textureDirectory}/{Name}.wct` (falling back to `{sharedTextureDirectory}/{Name}.wct`), splits solid from see-through per triangle, sets `DisableCulling`, and handles cut-out alpha. Its name is lobby-specific but the class is not.

**Axis swap:** `LobbyModel` builds vertices as `new Vector3( pos.X, pos.Z, pos.Y )` — the `.MD2` files are Y-up and the engine is Z-up. `Offsets` swaps the same way (`M41, M43, M42`). So `base.MD2`'s raw Y range (-11..99) is engine **Z** (height), and its raw Z becomes engine Y.

**NORMALS ARE NOT SWAPPED THE SAME WAY, and this will catch generated geometry.** `LobbyModel` swaps model *positions* but passes `.md2` normals through untouched, so normals stay in the file's Y-up space — and `content/shaders/test.shader` compensates for that itself:

    vs_out.vWorldNormal = mat3(g_oUbo.g_mModel) * vec3(normal.x, normal.z, normal.y);

So **anything that computes its own normals in engine space must hand them over with Y and Z already exchanged**, or that line turns them on their side: a flat ground's `(0,0,1)` becomes `(0,1,0)` and level land is lit as though it were a wall. This was a real bug in `ParkGround` — the ground rendered dark and muddy — and **it is invisible in a build, because nothing is wrong except the picture.**

Related: `vAmbient = g_flAmbient > 0.0 ? g_flAmbient : 0.4` — world draws leave `g_flAmbient` at zero and take the shader's flat 0.4, so geometry is never unlit. **If something looks black, suspect the normal, not the light.** And the shader's ambient is a single float, so `ThemeEngine.AmbientLightLevel` (a colour, 0xFF555568) cannot be applied without changing what every draw is handed.

**Careful:** `Vertex.Position` is **`OpenTPW.Vector3`**, not `System.Numerics.Vector3`, and both are in scope in any file that uses the engine — a plain `using System.Numerics` makes `Vector3` ambiguous.

## The inherited `VM/` code, and what the binary invalidates in it

`source/OpenTPW.Files/Formats/Script/Opcode.cs` holds **exactly 106 names** that match the binary's table. `source/OpenTPW/VM/` holds `Instruction`, `Operand`, `Branch`, `RideVariables` and, in `Handlers/`, **27 implemented handlers**. Those are worth keeping. But the inherited runtime does not run and must not be treated as a foundation: nothing constructs a `Ride`, and if anything did, `RideVM`'s constructor would throw, because it writes `Variables[VAR_RIDECLOSED]` (index 6) into a `List<int>` it has just created empty.

What the binary invalidates:

- `RideVM.VMFlags` (Sign/Zero) models a register the engine does not have; `Math.Test` and `Math.Compare` set those flags instead of a last result.
- **`Logic.BranchPositiveValue` branches whenever Sign is clear, so it branches on ZERO too**, where the binary requires strictly greater than zero. The docs page describes PV the same wrong way.
- `Math.Sub( valueA, valueB, dest )` follows the docs' operand order and is therefore wrong: the destination is operand 0.
- `Logic.JumpSubRoutine`/`Return` use a **`Queue<int>`** (FIFO); the engine is LIFO.
- **`RideVM.BranchTo`'s `value * 4 + firstOffset` conversion is simply wrong**, and its "HACK" comment describes a problem that does not exist.

`Ride.cs`'s path shape joins with a **backslash** and accounts for only one of the two scripts an archive can hold.

## Animation loading in OpenTPW versus the engine

`LobbyModel.LoadAnimations` reads role `M` only — one role in twelve — taking the bare `{stem}M.md2` as well as the numbered run, **under the engine's own condition: the bare file only where the numbered run came back empty.** The other eleven roles reach a park thing through `RideAnimations`, which is handed to it rather than probed. `ParkObjects.PoseAsBuilt` loads `{stem}c.md2` and calls it the construction clip, and that is **animation id 0**. All twelve roles are read for a park thing, by `RideAnimations.Load`, and a channel names its clip by role and entry (`RideAnimations.Clip`).

**Bare-only-M models outside `levels/`: zero**, so the bare-file probe cannot touch the lobby. But **91 models ship more than one numbered M clip**, and **8 of them are lobby models** — `*_gate` with 3 and `*_isle` with 2, in all four themes — plus the advisor with 15. `MeshAnimator` cycles every clip in turn; `MeshRotator` caps at `ClipsUsed = 2` and reads M1/M2 as a gate's open and shut, which is deliberate, screenshot-verified lobby behaviour. **So a channel must be something a park model opts into, leaving lobby playback exactly as it is.**

Known divergences still open, both in lobby playback, which a park thing does not use: `MeshAnimator` and `MeshRotator` keep private clocks, and play `FirstFrame..LastFrame` where the engine plays **0 -> declared span** (159 clips disagree). For a park thing the per-clip hide list is still unread, and `PoseAsBuilt` hand-rolls its effect after the build.

`AnimationFile.VisibilityTrack` already matches the engine's visibility rule exactly. `AnimationFile.RotationTrack.Ease` now obeys the easing table. The per-clip hide list is **still unread by us**.

**The gate's numbered clips differ by theme:** jungle and hallow ship `gatesm1`, `gatesm2` and `gatesm3`; **fantasy ships none, and space four** (`gatesm1..m4`). Fantasy ships `gatese/gatesi/gatesm/gatess.MD2` and no numbered clip; `gatesm.MD2` is real at morph 2, frames 0..50.

---

# Instruments and preserved artifacts

**Do not re-run the agents or the probes to get this back.** The scratchpad these were produced in is tmpfs and does not survive — that is how the `.RSE` corpus was lost once already. Copied to the harness folder (its path is in `CLAUDE.local.md`):

| File | What it holds |
|---|---|
| `animation-role-verdicts-2026-09-15.json` | the 8 adversarial verdicts with their addresses and byte encodings, plus the completeness critic's eleven findings in full (12,211 characters). The one refuted claim is keyed `absent`. **Read this before re-deriving the role decode.** |
| `animation-role-journal-2026-09-15.jsonl` | one line per agent, if a verdict needs its working |
| `animation-span-probe-2026-09-15.txt` | the header-vs-keys measurement over every clip under `levels/`: which files disagree, by how much, and the named examples |
| `animation-validity-probe-2026-09-15.txt` | validity vs span, and the ten items whose numbered walk is truncated by a clip `TryLoad` rejects |
| `animation-channel-verdicts-2026-09-15.json` | 11 channel claims, one skeptic each: 8 confirmed, 3 refuted (127 KB), with the journal beside it |
| `channel-seams-2026-09-15.json` | the five-reader workflow behind the six decompiled channel functions (128 KB, 74 findings and a completeness critic) |
| `md2dump/` | the whole corpus already extracted: **1,796 `.md2` files** (1,780 from the 306 level archives, plus the advisor's 16), one directory per archive keyed `theme__folder__name` so nothing collides. **Sweep this tree rather than rebuilding it** |
| `rolecensus.py` | applies the engine's own probe rule (numbered from 1, bare only if that run found nothing) to every archive's own name table — no decompression needed. Pass `--by-archive` to reproduce the older, narrower per-archive number |
| `rsewalk.py` | carries the `.RSE` format, the 106-entry table and every check above. **Re-run it rather than writing another.** It extracts with a per-wad subdirectory — keying output by the wad's own folder loses the theme (every script sits in a `rides`/`shops`/`features` folder) and silently collapses 262 wads into 203, overwriting scripts |

**`wadcat` is not on PATH**: its location is in `CLAUDE.local.md`, and it is **the DEBUG build, which matters.** A stale `bin/Release` copy is still on disk and its own usage line offers only `--list|--id|--bounds|--meshes|--anim|--cat`: **no `--dump`, no `--field`, no `--heights`**. Reaching for the Release path and finding `--dump` missing **looks exactly like a broken tool and is not one** — it is an out-of-date build. Its interface is `wadcat --list|--id|--bounds|--meshes|--anim|--field|--heights <wad>…` or `wadcat --cat|--dump <SUFFIX> <wad>…` - **the suffix comes first**, there is no `--out`, and **`--dump` is the one that EXTRACTS files**, into the working directory. Invoked wrongly it does nothing quietly, so "no scripts were dumped" reads exactly like "these archives carry no scripts". `--cat` writes nothing to disk: it prints every matching member to stdout behind banners, and has produced a false finding. `wadcat --anim <wad>` prints each clip's readability and its rot/morph/uv/pos/vis counts, which is how "what does this clip actually drive" is answered. Note `wadcat` is built against **our own `AnimationFile`**, so its `frames a..b` is the KEY-derived span and its `readable` is our `IsValid`; **it cannot independently check a declared span.**

A third read method — `memory.getBytes` into a Jython bytearray — **silently returned zeros and was discarded as a dead instrument** rather than believed. Two agents hit that same bug; no conclusion here rests on it.

**Three clip totals are all correct and answer different questions. Do not read a mismatch as an error:** **1085** clips summed PER SCRIPT (308 `.RSE` files across 262 archives; an archive with several scripts counts its clips once per script); **910** per archive-named item (274 of them); **1273** per base model across all of `data/` (445 of them). Likewise the item census: keyed on **base models** — any `<stem>.md2` with at least one role file beside it — the answer is **197 of 445**; the older "88 of 274" keyed each item to its archive's basename, which is invisible to every archive holding more than one base model (`terrain.wad` holds `base.MD2`, `queue.wad` holds every queue piece, and the go-kart and water-ride archives hold a track segment each).
