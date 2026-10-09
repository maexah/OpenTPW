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

Attribute values seen: 0 (empty), 1, 3, 8 (park entrance), 17 (0x11, the bus/road approach, 490 cells), 128, 144 (0x90, road/bus/crossing, 48 cells), 148 (0x94, ticket booth, 14 cells). They look like a bitfield. **Values 17, 144, 148 and 8 occupy byte-identical positions in all four parks** — the fixed infrastructure every park inherits. The rest differ per park: the 0/1/3 cells, jungle's four 128s (x 51..54, y 57) and one cell of 16 in fantasy (27,17).

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

`basem.MD2` reports `IsAnimation: True` with 0 meshes, exactly as `ModelFile` documents. It is `readable True` with **11 UV tracks over frames 0..100** — UV scroll is how this game moves water, as `Jun_isleM1` laps the lobby island's shoreline; OpenTPW's park log binds them to eleven meshes, the nine `surface*`, `Object12` and `falls02`, the waterfall (`4f307bb`). **Only jungle ships a `basem.MD2`**; fantasy, hallow and space ship `base.MD2` alone.

**A first park render needs no new file format**: `base.MD2` through the existing `ModelFile`, its textures through the existing WCT/WAD path, and a camera. `.MAP`/`.TCT` are needed for gameplay (attributes, editing, path tiles), not for the first picture; `base.lnd` is not needed at all.

## `.LND` — procedural-texture source, not the landscape

Header: byte 0 = 3, a version the engine checks (`FUN_00560ff0` refuses any other; `park-engine.md`, "The blocking unknowns", 6), 10 bytes of per-park values, then uint32 384, uint32 344, uint32 4 at offsets 11/15/19 — **the same 384/344/4 in all four parks** — then 4-byte quads. There is a section of 384 x 344 x 4 bytes = 528,384 somewhere in the middle, and ~1.8 MB beyond it. Sizes are park-specific (jungle 2,417,116; fantasy 2,196,802; hallow 2,447,361; space 2,241,690). The heights are not here: they are a block inside `base.MD2`, read by `HeightfieldFile` — see `park-engine.md`, "The heightfield lives inside base.MD2". The engine loads `base.lnd` only when `DAT_007a1a8c & 0x2000` is set (`park-engine.md`, "base.lnd is not the heightfield").

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

**The peep simulation's constants live here, not in the exe.** The block at `0x00785000` that the exe's peep code reads is **all zeros statically** — it is filled by the `BalanceLoader.cpp` parser from this file at load. So the tunables were never something to reverse engineer, and `ParkBalance` (which layers base + theme) can read them: `Balance.Int( "PeepInfo.ExitLevel" )` works. The keys are self-documenting — the file carries trailing comments like *"toilet level above which peep is 'desperate'"*. **Themes barely touch it:** the theme `Standard.sam` files override only `PeepInfo.ExcitementToCostDivisor` (fantasy and space, 4 -> 5), and jungle's `Easy_Standard.sam`, read over both for Instant Action, changes two peep keys: `AveragePriceMultiplier` 1.25 -> 1.5 and `ExpensivePriceMultiplier` 2.0 -> 2.5.

### The parks really are re-skins

All four `Standard.sam` files declare **identical** `MapInfo` (heightfield 95x84 at 0,0; FixedItemOrigin 48,17), identical `LightNormal` (0.4, -0.8, 0.4), identical `WaterStartPos` (380, -5, -180), identical `AmbientLightLevel` and `DirectionalLightLevel`. **Only `ThemeEngine.FogColour` differs.** One park engine serves all four — build it once, properly.

### The ThemeEngine colours are 0xAARRGGBB

    FogColour  jungle/fantasy 4774136    = 0x0048D8F8 -> R72  G216 B248 (pale blue; cf lobby 0x44DDFF)
               hallow         1122884    = 0x00112244 -> R17  G34  B68  (storm dark)
               space          16286768   = 0x00F88430 -> R248 G132 B48  (orange)
    AmbientLightLevel         4283782504 = 0xFF555568 -> R85  G85  B104
    DirectionalLightLevel     4294967256 = 0xFFFFFFD8 -> R255 G255 B216

Alpha is 0 on the fog values and 255 on the light values; every channel reads sensibly, which is the evidence for the byte order. `hallow` also ships a commented-out alternate ambient/directional/LightNormal set — **do not "restore" it.**

**How the original uses them** (`park-engine.md`, "The lighting model"): read once at the park load (`FUN_004080e0`, state 9) and never again. Every vertex gets `AmbientLightLevel` as a colour plus `DirectionalLightLevel` × −(d·n), d being `LightNormal`, the direction light travels; each channel is clamped (`FUN_005741b0`). Level ground and sun-facing faces saturate to their texture's full brightness. No time of day changes any of it.

## `Easymode.TPWI` — the Instant Action park

Container: the preamble laid out in `park-engine.md`, "The save container" (a u32 version, 400 here and 500 in a player save; the loader's fields, FileFormats `saves.md`, "Header"; the `BILZ` header), then **a single zlib stream at file offset 0x629** which inflates 38,479 -> **1,608,309 bytes** with no trailing data. Only `jungle` ships one, which is why Lost Kingdom is the Instant Action park. Same container family as `.TPWS` (player park saves). OpenTPW inflates the `.TPWI` with `SaveReader` and walks its payload with `ParkWorld`; the game opens no `.TPWS` yet, though harnesses read them with `SaveReader`.

The inflated payload names the park's features by path — `data\levels\jungle\Features\gates\`, `\Features\bus\`, `\Features\toilet\`, `\Shops\coconut\`, `\Rides\bouncy\` and more — so the gate is a save-placed object, not part of the terrain model.

## Formats that are not the problem

`test.tpt` (jungle + space only, 236 bytes) is a weather table: dword chunk count 6, then per chunk a 4-byte tag, 4-byte size, payload — `WTHR`, `TEMP`, `WNDS` (wind speed), `WNDD` (wind direction), `CLCV` (cloud cover), `ADSR`; each carries a count then five (value, weight) pairs. Decodable by inspection; named "test", probably vestigial.

`scape.omp` (409-590 bytes, one per park) is an `OBJ_` record file ending in an `INCL` chunk (count, then length-prefixed strings) naming the C++ headers it was built from: `D:\Park2\data\Particle\par_lib.h`, `SfxEvent.h`, `JungleEvent.h` — so it binds particle and sound events to the theme. Small; low priority.

## Buildable items: the per-item archive

An item is a `.wad` under `features/`, `shops/`, `rides/` or `sideshow/`, standing in for a directory of its own name, and everything inside is named after that stem: `<stem>.sam` (its description), `<stem>.MD2` (its model), `<stem>.hmp` (its height over each cell of its footprint, with the footprint's marks: FileFormats `hmp.md`), `<stem>.sgn` (a name board — rides, and the `gates` and `sign1` features), plus `textures/` and `stexture/`. Jungle holds **67** items across those four folders. **A coaster WAD also carries `GTexture/`**, the track's textures (FileFormats `sam.md`): the coaster loader `FUN_0042fad0` formats `"%s\GTexture"` (`0x0074cb8c`) and hands it to the track builder `FUN_0042fd90`.

**An item ships only the art unique to it.** Everything else comes from the theme's shared archives — `sharetex.wad` (full size, pairs with `textures/`) and `ssharete.wad` (low detail, pairs with `stexture/`), 116 members each, and **all four themes ship both**. Without that fallback the eleven objects Lost Kingdom places were missing **40 distinct textures** and drew the not-found art on most of their surfaces. Every missing name was present in both archives.

**An item's footprint is the BOX its `Info.Shape` picture is drawn in, not the cells marked inside it.** `4x4rock` draws 14 stars in a 4x4 box, `5x5rck` 23 in 5x5, and `ground`/`groundc`/`mystery` draw none at all — yet every one of the 70 jungle item archives (those 67 and the three in `upgrades/`) has a `.hmp` whose length says `48 + 27 * (width * height)`. **`gates` is the only jungle item carrying `Engine*Override` keys**, and it needs them: its picture is a single cell where its real footprint is the 6x3 its own `.hmp` declares.

The model is authored with its **footprint's corner at its own origin** — a 1x1 toilet's floor spans 0..10, the 3x3 fountain's 0..30, the 2x2 staff room's 0..20 — read from node `WorldTransform`, never from bounds, which are node-local and say only how big a mesh is.

**Every item's model opens with a flat floor plate as wide as its whole footprint** — `J_WC`, `wf_floor`, `js_base`, `cn_floor01`, `jb_floor`, `jc_base` — and all 44 footprint cells carry a real ground index, so the ground must skip them or the two fight for the same depth.

## `.sgn` — one layout, walked in order

`SignFile` reads all 84 signs by walking the header in the engine's own order (`FUN_005ec3a0`): 61 leave the
artwork byte at `0x08` clear, and three (jelly, zob, C_SCAT) omit both ink blocks, which is what made the header
look like it had two fixed sizes. The layout is in the FileFormats clone, `formats/sgn.md`, on its
`docs/sign-format-corrections` branch (not yet on master). The `.sgn` is always named after the
item's own folder — **80 of 80 across all four themes** (72 rides, and `gates` and `sign1` in each theme's `features/`).

## `.RSE` — the ride-script container

Magic and version are uniform across the whole shipped corpus, **checked not sampled**: 312 wads scanned, 308 scripts extracted, and **308 of 308** carry magic `RSSE` and version `0x00010F51`, none malformed. Sizes run 88 to 2,172 bytes, mean 577. All 308 parse to their last byte and walk instruction by instruction to their last word.

    0x00  char[4]   "RSSE" (0x45535352 LE)
    0x04  int32     version 0x00010F51, compared against DAT_00879190; a mismatch only WARNS
    0x08  int32     variable count            -> loader field +0x23, allocates count*4 ints
    0x0c  int32     count                     -> +0x15   (the STACK SIZE)
    0x10  int32     50 in every shipped file  -> +0x25   (the TIME SLICE, an instruction budget)
    0x14  int32     LIMBO slots, 8 bytes each -> +0x16, array in field 9
    0x18  int32     BOUNCE slots, 16 b. each  -> +0x19, array in field 0xa
    0x1c  int32     WALKON slots, 32 b. each  -> +0x1f, array in field 0xb
    0x20  byte[16]  four dwords the loader reads and DISCARDS - always "Pad Pad Pad Pad "
    0x30  int32     script length in DWORDS   -> +0x14
    0x34  int32[]   the script body           -> +6
          int32     string blob length        -> +0x24
          byte[]    the blob: NUL-terminated strings, referred to by BYTE OFFSET
          ...       one length-prefixed name per declared variable (length includes the NUL)

**The variable names at the end are the trap.** The loader allocates the variables' storage from the count and never reads their names, so a reading that stops at the string blob leaves a tail it cannot explain. Off that reading **98 of 308 looked well-formed and 210 looked broken — and the 98 were exactly those declaring no variables.** Parsing the names accounts for the last byte of all 308. **74 distinct names corpus-wide**, every one `VAR_*`; the twelve `RideVariables` lists are the first twelve of 129 scripts, in that order, which is why `VAR_RIDECLOSED` is index 6.

**Where the scripts live:** none are loose on disk — every one is inside `data/levels/<theme>/{rides,shops,sideshow,features,upgrades}/*.wad`. `EventMap.RSE` appears **28 times** out of 308, so it is a minority of archives, not most. 38 names recur across themes (counted case-insensitively), eleven of them in all four (`EventMap`, `bus`, `ferry`, `seaplane`, `lights`, `gates`, `end`, `AnimCtrl`, `sign1`, `wateride`, `gokarts`). Extensions are **305 `.RSE` to 3 `.rse`** and case is inconsistent in the data (`Coaster1.RSE`, `Monkey.rse`, `child.RSE`), so **any lookup must be case-insensitive** — mandatory here in a way it never was on the original's file system.

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
| `FUN_00461f10` | - | item loader; composes `"%s%s%c.md2"` and `"%s%s%c%d.md2"` — directory, stem, letter, optional number. Also the source of a model's channel count (its allocation argument). **It clears header bit `0x4`** (relative animation, FileFormats `models.md`) and logs `0x0074d2c4` ("Ride %s has relative animation incorrectly set") unless its second argument has `0x20000` (`0x0046211a`..`0x0046214b`); `FUN_004629d0` sets that from bit `0x1000000` of its fifth (`0x00462ac1`), and its other call (`0x00462c07`) passes 0 and always strips. The track loader `FUN_0042fad0` passes `0x1013405` as `FUN_004629d0`'s fifth (`0x0042fca7`, `0x0042fd1e`) and keeps the bit; the car loader `FUN_00430130` (`0x00430150`) and the `SupplementalMeshes` loop (`0x00414373`) pass `0xc0` and would strip it. **Open:** which of the 25 bit-`0x4` models `FUN_00430130` loads, so whether any shipped car loses it | read |
| `0x006fe6bc` | - | the twelve-entry letter table, **stride 8** | `MOV [ESP+0x14],0x6fe6bc` at `0x004622fb`, `MOV [ESP+0x20],0xc` at `0x00462303` |
| `0x74d2b4` | - | format string `"%s%s%c%d.md2"` | read |
| `0x74d2a8` | - | format string `"%s%s%c.md2"` | read |
| `FUN_0044a220` | - | the probe: whether the composed name exists (one call site, `0x0046240e`, with flags `DAT_007a445c ? 2 : 0`). Flag 2 looks the basename up in a preloaded name list (`FUN_00461820`; −1 when `DAT_007a445c` is 0); without flag 4 that answer stands, a miss included (`0x0044a274`); the file is opened and closed (`FUN_0046f120`) only when flag 2 is absent, or when flag 4 is set and the list answered nought. It loads nothing; the loader is `FUN_0046dcf0` (`FUN_0046d6d0`, then `FUN_0045ba70`, `FUN_0046ead0`) | Ghidra, Q194 |
| `FUN_004629d0` | - | loads a whole second model+animation set as `"p%s"` into `+0xd0` when its flags carry `0x20000` (`0x00462bd1`). A leading `P` is a **prefix**, not a suffix. **It is the model the object windows and the buy screen preview** (`park-engine.md`, "The object window's preview") | read |
| `FUN_00463060` | - | build path: checks role 0 exists, triggers it, then starts `0xd` at once (not a clip — it binds nothing; it sets the freeze flag `0x2` and re-stamps the channel's timers, so role 0's clip holds at frame nought) | read |
| `FUN_004647a0` | - | save load: overwrites every animation channel with the saved state and restores the per-node flag words with it | read |

    id 0  C      id 3  L      id 6  E      id 9   B
    id 1  D      id 4  S      id 7  U      id 10  R
    id 2  I      id 5  M      id 8  W      id 11  O

Probe rule: `<prefix><stem><suffix><n>.md2` with **n from 1 upward** until a probe fails, and **if no numbered file exists** it falls back to the unnumbered `<stem><suffix>.md2`. So `bouncyb/c/i/r` are four roles with one clip each and `JunsprayM1..M6` is one role with six.

**The exe assigns the letters no meaning** — only ordinals — so "C = construct" is safe (the build path hard-codes role 0) and the rest are inference. Only the letter is ever read (`(int)*pcStack_42c`). The second dword of each table entry runs 1..11 and then a float, so **it is not established as part of the table and nothing should lean on it**.

**Cross-validated without the binary**, against the ids used by the 72 ride scripts whose own wad ships each letter: `c` -> id 0 at **1.00 against a 0.00 base rate**; `i` -> id 2; `l` -> id 3 (0.91 vs 0.13); `s` -> id 4 (0.94 vs 0.03); `e` -> id 6 (0.93 vs 0.02); `b`/`r` -> ids 9 and 10, which co-vary because those two files travel together. Six letters land exactly where the table puts them, from data that never touched the executable. `m` shows no signal only because id 5 is used by 55 of the 72 wads, so it cannot discriminate.

**REFUTED: an id is not an index into the `M<n>` clips.** Of the 72 ride wads whose script names a literal id, **all 72** have a highest id at or above the number of `M<n>` files they ship: `gokarts` ships **none at all** and triggers ids 0 and 5, `jelly` ships none and reaches 10. `M` is simply the letter of **one** role — id 5 — and the one whose files most often carry numbers: 278 numbered `M` files, against 105 across eight other letters (`B` 40, `L` 23, `I` 11 and fewer for the rest).

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

The trigger reads the block's `+0x04` and `+0x08` as **integers** — read as floats they give denormal nonsense — and multiplies the difference by the float **33.3333 at `0x006fec08`** (`0x42055555`, 33.33333206), the nearest float to 1000/30 rather than the exact value.

**Measured over the 1,278 clips in the game that carry a block: every one declares a start of nought and an end of at least one, and 164 disagree with what their keys span** — 127 short, every one reading as no span at all through our own reader, which rejects clips carrying no rotation, UV or readable morph track (89 of them carry no track at all), and 37 long, where the keys run past the declared end and the engine never plays them.

**`FUN_00474070` is NOT this function**: it multiplies `clip[+8]` alone with no subtraction, and its one caller is in the advisor's range. **Do not cite it for ride timing.**

## The animation channel — `AnimTimeControl`

The struct has a shipped name: `FUN_00464580` is a debug dumper that prints every field by name, and it indexes as `(channel - model[+0x10]) / 0x38`, so the exe's own arithmetic confirms the stride. **Use these names.**

| Offset | Original name | What it is |
|---|---|---|
| `model+0x0e` | - | ushort channel count |
| `model+0x10` | - | channel array, **stride 0x38** |
| `channel+0x00` | `Flags` | flag `0x1` = loop (`LOOPANIM` at `0x00552beb` passes flags 1; `TRIGANIM`/`WAITANIM` pass 0, all on channel 0); `0x2` = freeze at frame 0; **`0x4` = held on the last frame**, set with `0x10` as `0x14` by the end-of-clip hold (`0x00472fe8`; `0x10` goes at `0x00473a19` when the channel is posed); `0x40` selects the alternate frame snapshot. `0x2` and `0x4` are cleared only by `FUN_00472f60`'s `AND AL,0xf9` when it starts a loaded role's in-range entry over a channel that is not idle (`0x0047302b`, `0x0047303b`), and by the whole-word copies (the RSYS restore `0x00464bcd`, the instance copy `0x004558fc`); a pseudo-role ORs its bits in without clearing, and a start on an idle channel, or of an entry past the count, keeps a stale one |
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

`FUN_004732a0(model, role, entry, flags, speed, channel)` starts at once only if the channel is idle, its running role's slot is not loaded or its running entry is past the count, it is finished, it is flagged `0x6`, the role is 13/14, or flags carry `0x2` (the gates `0x004732e5`..`0x00473344`); **otherwise it QUEUES** and answers the remaining time plus the new clip's length (or plus a flat 1000). The two terms are **truncated separately**. Queue clear resets `+0x24/+0x28/+0x30` and deliberately **leaves `+0x2c` stale**. **`flags & 0x2` means "start now AND clear the queue"** — that block is reached only on that path, and it writes `+0x24 = 0xc`, `+0x28 = 0`, `+0x30 = 0`.

**The "+1000" fallback is not a simple if-then.** Reaching `ADD ESI,0x3e8` at `0x004733e7` needs **eight** earlier gates to pass: the currently-playing entry must itself name a real clip, its timing must satisfy `[+0x20] <= [+0x1c]`, `[ESI] & 6` must be clear, `param_2` must be neither 13 nor 14, and `param_4 & 2` must be clear. **In the ordinary idle case — nothing playing, which is every fresh script — `0x004732ef` diverts to `LAB_0047340b` instead**, and the answer is `FUN_00472f60`'s, or a **flat** 1000 with no remaining-time term added. So "role absent gives playing-time + 1000" is true only mid-animation.

**"Finished" is judged on `+0x20` as the last per-frame advance left it** (`0x00473315`..`0x00473320`): nothing brings a channel up to the trigger's moment. And no advance leaves a channel past its end — a promotion's and a loop's carry are clamped to the new clip (`FUN_00472bc0`, `0x00472c12`..`0x00472c23`), and a hold or a stall pins the total — **so the finished gate fires only for the idle default's own trigger** (`FUN_00473490` stores channel 0's elapsed unclamped at `0x0047351c` just before it; never on a thing's model, below), **and a script's trigger onto a running clip that is neither held nor frozen always queues.**

**A trigger naming a role the model lacks does more than answer 1000**: `FUN_00472f60` calls the rest-pose restore `FUN_00472310` and parks the channel at role 12 (`0x00473121`..`0x00473139`), without clearing `0x2` or `0x4`. **An entry past a loaded role's count does not park it**: the start path never bounds the entry (`0x0047300c` only skips the clear), reads `[clip table + entry × 8 + 4]` past the role's clips, sets `AnimID` to the role (`0x0047308f`..`0x004730c8`) and answers 1000. Eight shipped references name a role their archive lacks, each at entry 0; no literal reference names an entry past a loaded role's count, and eleven name the entry by a variable, none in Lost Kingdom.

### Who advances a channel, and when

`FUN_004735d0` advances one channel, called once per channel from `FUN_00473c70` (stride 0x38, correct), called by two routes. **A model on screen** is advanced and posed from its scene-node callback `0x00463030` (installed by `FUN_00463060` at `0x004638bd`) which calls `FUN_0044e380( model, 1 )` and then marks it `+4 |= 0x200000` (`0x0046304d`); **every other model** is advanced, not posed, by the per-frame model sweep `FUN_0044e410( 3 )` with `( 0, 8 )` (a model whose header `+0x30` carries `0x4` still has its node-lookup records' matrices composed by `FUN_0044ab30`, `0x00473e01`..`0x00473e1b`: 25 shipped models, all coaster parts; `ride-operation.md`, "How long a leg lasts, and where its ends are"), which skips and clears that mark (`0x0044e4a4`, `0x0044e4e0`) — the routine `0x0054e2b0` that the park's load hangs on the scene (`0x0054ecbc`) and the scene draw at `0x0054fb6c` runs through `FUN_00576a00` — **after the 31 ms catch-up loop's back edge at `0x0054f8da`**. The park's `FUN_0044e410( 2 )` at `0x0054fa96` advances only the model at `DAT_00790988`. It takes **no time argument**: it reads `DAT_007b496c` (or `DAT_007b4974` when channel flag `0x40` is set), a snapshot written once per frame by `FUN_00473440` at `0x0054f475` from the clock object at `0x785970`.

**Animation advances once per frame off one snapshot while scripts tick at 31 ms** — several sim ticks in one frame still produce exactly one advance. Confirmed by call graph: `FUN_004735d0` has **exactly one** caller, `FUN_00473c70` at `0x00473d2e`, and **not one** of that function's ten call sites is the script system `FUN_005516b0` or is called directly inside the 31 ms loop; the park's are `FUN_0044e410(2)`, `FUN_00429df0(0)` and `FUN_00429df0(1)` in the loop's tail, and `FUN_0044e410(3)` and `FUN_0044e380` (`0x0044e3cc`, from the scene-node callback) inside the scene draw, so both routes advance once a frame, after its ticks. Of the rest, `FUN_0044e510` advances one model of the state machine's own (called at `0x0054e72a`), and five advance a coaster's car models (`FUN_00430590` twice, `FUN_00430ed0`, `FUN_00432df0` twice); those the loop can reach, when a loaded save's layout is replayed (`FUN_00516380` at `0x0051669f`, down to `0x004311f0`) and when a model is built with flag `0x200` (`0x00463860`, through `FUN_004368f0`, `FUN_00436ac0`, `FUN_00434060` and `FUN_00433bd0` to `FUN_00432df0`). None is a placed thing's own model.

**Two candidates were checked and cleared, and either would have inverted this:** `FUN_00473440` runs once per frame *above* the loop, so it looks like the sweep, but only computes the frame delta into `DAT_007b497c`; and `FUN_00475360` does run inside the loop every 2nd tick, but is a periodic-task scheduler (`+0x7c` due time, `+0x80` interval) that calls none of this.

The consequence for OpenTPW: the advance belongs in the frame sweep, **not in the tick**. An advance in the tick leaves end-of-frame state the same, but a clip ending mid-catch-up promotes its queued successor early, where the next tick's instructions can see it.

**Two gates, and they invert the idle default.** `FUN_00473c70` touches nothing unless `model+0xa8` is non-zero, and the sweep will not call it unless `model+0x14` is non-zero. `FUN_00473e30` parks every channel at role 12 on load, and `+0x14` is raised by a start (`0x004731b4`..`0x0047320a`) or by the RSYS restore's recount of the channels below role 12 (`FUN_00472ee0`, called at `0x00464cde`), so a loaded park's restored loops play with no trigger. Where the default can run, it waits for a clip to finish; then it fires every frame channel 0 reads finished, three attempts at most, with flags 8 from the off-screen sweep (`0x0044e4b7`: no rest restore, no hide list) and 0 from the on-screen route (`0x0044e3c9`) and `FUN_0044e410( 2 )` (`0x0044e42f`), which lay the rest pose and apply the hide list. **It never fires on a placed thing's model**: `FUN_004dd500` builds one through `FUN_00463060` with `0x361` or `0x32f` (`0x004dd5b7`, `0x004dd64f`), whose low bit sets `+4 |= 4` (`0x00463190`..`0x0046319a`), and `FUN_00473c70` skips the default on `+4 & 0x8004` (`0x00473c83`); a loaded park rebuilds each with its saved creation flags (`0x0046424e` → `0x00464942`). So a thing's channel 0 changes only when a script triggers it. Where the default does run, `FUN_00473490` counts a **held** channel 0 finished once it has been held longer than its clip (`0x004734ea`..`0x00473524`).

**A newly built thing starts channel 0 on role 0, frozen** (`ride-operation.md`, the `FUN_00463060` row). OpenTPW's channels start idle, at role 12, so a `GETANIM_CH` before the first trigger would answer 0 there and 12 here. None asks that early: all five scripts that use it open with `WAITANIM 0, 0` at word 2 and three `TRIGANIM_CH` before their first branch, and their first `GETANIM_CH` is at word 176 or later.

**The idle default is role 5 entry 0 on channel 0**, restarted when channel 0 is idle or finished, gated on `(model+4 & 0x8004) == 0`. Entry 0 is a literal — **nothing in the engine ever advances to a role's next entry**, so cycling every `M` clip has no engine counterpart. `model+0x14` gates whether a model is advanced at all: it counts channels with role < 12, and a stopped model drops to nought and is never restarted — so an unconditional idle restart would start role 5 on every static prop in the park.

### End of clip is five outcomes, not three

A channel on role 0 found past its end is first marked `0x8` (`0x00473758`), and every advance of a channel on another role takes the mark off (`0x004738b2`; `saves.md`, "OpenTPW's writer, a channel's `0x8`"). The queue is promoted first, whatever the model's flag word holds (`0x0047375d`). Failing that, `model[+4] & 0x18` chooses: clear, replay when flag `0x1` else pseudo-role `0xe`; else if `0x8` clear, **stall** (pin elapsed to total, set `model[+4] |= 0x40400000`, defer a stop to the end of the frame, which parks the channel at role 12 with its pose kept, `0x00473a30`); else **stop and call out** to the dispose/reposition group. `FUN_00473e30` sets `0x10` on a thing's model whose only clips are role 0's (`0x00473f32`): 22 Lost Kingdom items, each running one `WAITANIM 0, 0` and nothing that reads the channel afterwards. The replay's speed comes from `+0x30` when non-zero, only falling back to `+0xc`.

**"Freeze" is not "stop posing".** `FUN_00472f60`'s `0xe` pins `elapsed := total`, so the clip is posed **once more at its true final frame** and held; `FUN_00471860`'s early return never fires for it. A rest-restore does exist (`FUN_00472310`, master -> instance, differential at a clip switch) and the freeze paths suppress it by passing bit `4`. **In blend mode (`model+0x30 & 4`) the rest pose is re-laid every frame and a finished channel snaps back** — the opposite of freezing.

**Continuity is a move of the start stamp, not an accumulator.** `FUN_00472bc0`: total is `(float)(uint)(clip[+8] - clip[+4])`; a reset stamps `+0x10`, `+0x14` and `+0x18` from one snapshot; an overrun instead sets `+0x10` and `+0x18` to `+0x14 - trunc(carry * 33.333.. / speed)` with the carry clamped to the new clip's own total. **A float delta accumulator cannot express this.**

### Channels are per-model, unbounded, and one function is buggy

Channels are per-model animation players, count at `model+0x0e`, array at `+0x10`, stride `0x38`, and **none of the three functions that index them bounds-checks against that count**. A model is built with as many as its creator asked for — one for scenery (eight call sites), five for a coaster's trains, a per-thing record field on the main path. **The channel count is not model data.** The corpus names channels 0 to 3; `jungle/rides/totem` uses 3 and `jungle/sideshow/junspray` drives channels 0, 1 and 2.

**`FUN_00473490` does not survey the channels.** `ECX` and the role are loaded **outside** the loop and the pointer is never advanced, so it tests **channel 0** count-many times, and it **writes** as well as tests — refreshing channel 0's `+0x14`/`+0x18`/`+0x20` count-many times and answering "all finished" from channel 0 alone. **It is a shipped bug**, and it matters only where the idle default runs, which is never a placed thing's model (above): there a multi-channel model whose channel 0 has finished is called finished whatever its other channels are doing, and gets role 5 restarted on channel 0 beneath them. The 3-attempt cap in `FUN_00473c70` is load-bearing too: with no role 5 the predicate stays true for ever.

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

The list lives at clip block `+0x1a` (count) / `+0x38` (pointer). **854 of 1,166 clips carry one, 12,872 entries in total** (859 of the 1,237 clips under `levels/` that carry a block, counted without the track-table check).

- **`FUN_00472d70` HIDES the incoming clip's list at clip start.** Each entry resolves by the same `idx < meshCount ? meshTable + idx*0xa0 : nodeTable + (idx-meshCount)*0x58` rule `ModelFile` uses, then sets flag `0x10` unless the node carries `0x80000000`. **Entries are NODE INDICES.**
- **`FUN_00472310` CLEARS `0x10` over the outgoing clip's list**, then restores `0x289` nodes from the master by copying matrix rows `+0x10/+0x20/+0x30/+0x40`, with arms for `0x1000` and `0x10000` and **no `0x20000` arm** — which is why visibility is never put back.
- The `& 4` id-search arm is a flag on the **MODEL record**, not on the clip. The default path is index-addressed and is all shipped data needs (**only 4 entries in the whole game fall past their model's nodeCount**).
- **`FUN_004726d0` recomposes hide state across every channel** (array at `+0x10`, count at `+0xe`, stride 0x38), clearing then re-applying per channel.

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

**For the arithmetic the destination is operand 0, and only a variable is written.** Read off the shared store tail at **`0x00554911`**, which `SUB` jumps to: it stashes the result in the scratch field `+0x48`, tests operand 0 for tag `0x40`, and **if it is not a variable the instruction returns with only the register written**; otherwise it writes `variables[op0] = result`. **`ADD`, `COPY`, `TEST` and `CMP` do it the other way round**: they test operand 0 first and leave through `NOP` before the register is touched (`0x00553e72`, `0x00551da3`, `0x00554185`, `0x00554225`), so they write `+0x48` only for a variable (`0x00553eab`, `0x00551e0b`, `0x00554197`, `0x00554235`).

    ADD <dest:var> <value>            variables[dest] += value        handler 0x00553e13
    SUB <dest:var> <a> <b>            variables[dest] = a - b         handler 0x00551e58
    DIV <dest:var> <a> <b>            variables[dest] = a / b         handler 0x00553f8a
    MOD <dest:var> <a> <b>            variables[dest] = a % b         handler 0x0055405c

**A zero divisor does not trap**: `DIV` and `MOD` compare the divisor against zero first and yield **0** (`0x00554127`), then store as normal. Each runs its own `IDIV` (`0x00554052`, `0x00554120`) and writes the register, `DIV` EAX at `0x00554054` and `MOD` EDX at `0x00554122`; they share the zero-divisor write and the tag test and store from `0x0055412a`.

**`SUB`, `DIV` and `MOD` all put the destination FIRST, and the published docs put it last.** The docs say `SUB/DIV/MOD <value> <value> <dest>`; the binary resolves operands **1 and 2** to values and stores to operand **0**. **`ADD` is the one the docs get right** (`<source>` doubling as the destination). **None of ADD, SUB, DIV or MOD touches the flags** — they write only the scratch field `+0x48` — so the docs' "the relevant flags will be set", repeated on all five arithmetic entries, is false.

**There is no flags register. `+0x48` is a last-result register, and the branches test it.** Most instructions that compute something write their result to `+0x48` **first and unconditionally** — the `SUB` and `MULT` tails and `DIV`'s and `MOD`'s own writes (`0x00554913`, `0x00555b4a`, `0x00554054`, `0x00554122`), `PUSH`, `POP` — and only then write `variables[op0]`, and only if operand 0 carries tag `0x40`. **The exceptions write it on some paths only**: `ADD`, `COPY`, `TEST` and `CMP` for a variable operand 0 (above); `GETANIM` and `GETANIM_CH` only when the script has a model (`+0xc8`, `0x00552de5`, `0x0055372e`), through `FUN_00473fb0`, and otherwise copy whatever the register held into a variable destination; `GETVARINCHILD` and `GETVARINPARENT` not for a literal operand 0, a child or parent id of nought, or an index at or past the target's count (`0x005553c3` and `0x0055556c`, `0x005553ce` and `0x00555577`, `0x005555b3`; a negative index is read from before the array, and an id naming no script reads through a null pointer at `0x005555ad`); `SETVARINCHILD`, `SETVARINPARENT` and `SETREMOTEVAR` only when their write lands (`0x005554e4`, `0x0055627e`); `BUMP` 1 and 2 and `TOUR` 3, 4 and 16 only for a variable operand 1 (`0x00554758`, `0x005547a4`, `0x0055446a`, `0x005546ce`); `TRIGWAITANIM` and `TRIGWAITANIM_CH` on their first visit only; `FORCEUNLIMBO`, which tests the tag first; `HUSH` and `HOP` not on a heap error ("The two stacks"). `WALKON` and `WAIT` never write it. The conditional branches compare `+0x48` against zero, **signed**:

    BRANCH_Z   0x00553afb  CMP [+0x48],0 / JNZ nop   -> taken when result == 0
    BRANCH_NZ  0x00553bf1  CMP [+0x48],0 / JZ  nop   -> taken when result != 0
    BRANCH_NV  0x00553b4d  CMP [+0x48],0 / JGE nop   -> taken when result <  0
    BRANCH_PV  0x00553b9f  CMP [+0x48],0 / JLE nop   -> taken when result >  0   (STRICTLY)

    TEST <var>           +0x48 = variables[op0]                     0x00554152
    CMP  <var> <b>       +0x48 = variables[op0] - value(op1)        0x005541a5
    MULT <dest> <a> <b>  variables[dest] = a * b, tail 0x00555b48

So `CMP a, b` then `BRANCH_Z` is "branch if equal", and `TEST v` + `BRANCH_NZ` is "branch if v is non-zero". **`+0x48` is the whole condition state** — no Sign bit, no Zero bit. **`CMP` is a SUBTRACTION, not a bitwise AND** as the docs page says: `SUB ECX,EAX` then `MOV [EBP+0x48],ECX` at `0x00554235`.

**`0x005567b4` is NOT an error exit — it is NOP's own handler.** It sits inside the dispatcher and is a bare epilogue (`POP EDI/ESI/EBP/EBX; ADD ESP,0x134; RET`). `END` is the instruction **three bytes earlier** at `0x005567b1`, which parks the PC first.

**Two different bail targets, and the difference matters.** A branch whose condition is false falls to `0x005567b4` (NOP — harmless, carry on). A branch whose operand is **not** tagged `0x20` goes to `0x005567b1` (END — **parks the PC**, stopping the script). Same for a destination that is not a variable: NOP, not END. **`COPY` is the exception**: it tests operand 0 before it fetches operand 1 (`0x00551da3`), so its `NOP` leaves the PC on operand 1, which the next dispatch refuses as *"RSSE: Bad instruction - missing parameter ?"* (`0x005567a3`) and parks — a literal-destination `COPY` ends the script. None ships; all 1,243 name a variable.

That skipped write is used on purpose. **351 shipped instructions store into a literal**, in 16 opcodes, counted over all 308 `.RSE` against each handler's own store (the Q166 decode):

- **95 leave the answer in the register for the branch after them.** On every path a conditional branch reads `+0x48` before anything writes it again: `LIMBOSPACE` 24 and `GETTIMER` 21 (every shipped use of each), `GETANIM_CH` 15 (every use), `COAST 2 0` 12 (every `GETQUEUE`, below), `INLIMBO` 4 (each theme's arcade), `MOD` 4 (`MOD 0 VAR_CAPACITY 9` in jungle and hallow `TourRide` and fantasy `twetours`, `8` in space `scitour`), `RAND` 4, `BUMP 11 0` 4 (each theme's water ride), `GETREMOTEVAR` 2 (space `zob`), `SUB` 2 (space `orbiter`), and one each of `SEC` and `MIN` (hallow's `Clock`, which waits for the real clock's top of the hour) and `WALKFLOATSTAT` (space `ZeroG`). 79 of them name operand 0; `COAST` and `BUMP` store into operand 1.
- **256 throw an animation's length away.** `TRIGWAITANIM` 132 (of 133), `TRIGANIM` 64 (of 74) and `TRIGANIM_CH` 60 (of 63) store the clip's length less 300, floored at 300, into operand 2 and the register, and none is read: every path from each meets another writer before a branch, bar the two fountains' (jungle's Round Fountain and fantasy's Fountain, `Fountain.RSE` and `fountain.RSE` word 2), which loop for ever holding it.

`SETVARINCHILD literal:0/1`, seven times, is no destination at all: operand 0 is the child's variable number. None of the 351 is corruption or a misaligned walk. **An interpreter must skip the variable write rather than throwing, and still leave the answer in the register.** (A reachability check that follows both sides of every conditional proves nothing about them either way.)

**Which operand is a destination, and what a literal there does**, handler by handler:

- **The register first, then the variable only when tagged, and carry on**: operand 0 of `SUB`, `MULT`, `DIV`, `MOD`, `GETTIME`, `RAND`, `POP`, `HOP`, `UNLIMBO`, `INLIMBO`, `LIMBOSPACE`, `UNBOUNCE`, `FORCEUNBOUNCE`, `BOUNCING`, `WALKGET`, `WALKFLOATSTAT`, `GETREMOTEVAR`, `GETCUSTPTCLCODE` (always nought, `0x005563fa`), `GETTIMER`, and `YEAR` to `SEC` (the real clock through `localtime`, not the park's calendar; `0x005565b2`..); `GETANIM` and `GETANIM_CH`, whose register is written only with a model; operand 1 of `COAST` ops 2 and 3, `BUMP` op 11 (`0x00554913`) and `FINDSCRIPTRAND`; operand 2 of `TRIGANIM`, `TRIGANIM_CH`, `TRIGANIMSPEED`, and of `TRIGWAITANIM` and `TRIGWAITANIM_CH` on their first visit. A literal `UNLIMBO`, `POP`, `HOP`, `UNBOUNCE`, `FORCEUNBOUNCE`, `WALKGET` or `COAST 3` still takes what it takes, and keeps it only in the register. `HOP` writes neither on a heap error, `POP` has parked on a stack error, `FINDSCRIPTRAND` with no match writes only nought to the register, and `TRIGWAITANIM_CH` then ends the script through its one-short rewind whatever the destination.
- **The destination tested first, so a literal writes nothing, the register included, and carries on**: operand 0 of `ADD`, `FORCEUNLIMBO`, `GETVARINCHILD` and `GETVARINPARENT`; operand 1 of `BUMP` op 2 (`0x00554777`) and `TOUR` ops 4 and 16 (`0x0055448f`, `0x005546a4`). None ships with a literal.
- **Tested first, and the script ends**: `COPY` (above).

**OpenTPW stores each built one the way its handler does** (`RideScript.Store`, and the cases that test first), register and all, checked opcode by opcode against the disassembly, except where it declares otherwise: a model-less `TRIGWAITANIM` walks past writing neither (the engine floors the length to 300 in the register, `0x00552d61`), and a `COAST` with no ride state is counted and leaves the register alone. 344 of the 351 are in built opcodes. The other seven fall to the counted default, which leaves the register as it stood, so the branch after each reads a stale value: `BUMP 11 0`, `MIN`, `SEC` and `WALKFLOATSTAT`. `STARTSCREAM` writes the register too, with the sound's handle or nought (`0x00555ee3`), and `StartScream` does not: no shipped branch reads it, since each of the 40 reaches a `TEST` or a `COPY` first.

### The two stacks

**One stack array, two stacks, growing toward each other.** Both share the base at `+0x20` and the size at `+0x54`, and each has its own index:

    +0x40   JSR / RETURN / PUSH / POP    starts at stackSize-1, JSR DECREMENTS (grows DOWN)
    +0x44   HUSH / HOP                   starts at 0,           HUSH INCREMENTS (grows UP)

The loader (`FUN_005587f0`) allocates the array, `stackSize * 4` bytes and not zeroed, **only when the header's size is non-zero** (`0x00558956`); with `#setstack 0`, as 253 of the 308 scripts have, `+0x20` stays null. Its otherwise unexplained `field[0x10] = field[0x15] - 1` (`0x00558c04`) is `index = stackSize - 1`, the initial top of a downward stack, and `field[0x15]` is the header's **stack size**. `+0x44` needs no initialiser because the loader zeroes the whole script object. Every script, a child or a sound script too, is made either by the loader, which starts it at word 0 with both indices and the result register fresh, or by the save reader. The save reader `FUN_005597a0` reads the whole struct back (`0x00559a00`), both indices and the result register with it, restores the body from the save rather than the `.RSE`, takes the array from the record's first block after the body, and rebuilds `+0x54` from that block's length (`0x00559af1`) without checking the saved `+0x40` against it; the FileFormats docs, "The ride script module", has the bytes.

**Nothing keeps the two apart.** `HUSH` never reads `+0x40`, and `JSR`, `RETURN`, `PUSH` and `POP` never read `+0x44`, so each end sees only its own index: a `HUSH` writes over a live return address once `+0x44` reaches `+0x40 + 1`, and a `JSR` or `PUSH` writes over a value `HUSH` left, which a `RETURN` or `POP` can then take. `HOP`'s second test, `+0x40 > +0x54` (`0x00553de4`), is never true for a script the game loaded or saved: every writer keeps `+0x40` in `-1 .. stackSize-1` (`JSR` `0x005539f4`, `RETURN` `0x00553a48`, `PUSH` `0x00553c86`, `POP` `0x00553cfb`, the loader), and the save reader gets back an index those kept. It is not a collision guard.

**The call end.** Every error here logs *"RSSE: Stack Error"* (`0x765c2c`) through the bare-`RET` logger and parks the PC at `-10000`, which kills the script at the tick — unless the instruction then writes the PC again.

- `JSR` (`0x005539a9`) takes the PC, already past its operand, **ORs in the `0x20000000` label tag**, writes it to `stack[+0x40]` and decrements. With no stack, or no room, it parks (`0x00553a07`) **and then jumps anyway**: the operand's tag is tested after either arm, and a label goes into the PC (`0x00553a24`). What follows depends on the error. With **no stack** the subroutine's `RETURN` fails too and ends the script. With a **full** stack (`+0x40` = -1) that `RETURN` passes its tests and pops `stack[0]`, the return address of the call still open, so it returns **one frame too far** and skips the rest of the enclosing subroutine; the script lives. A non-label operand leaves through `NOP` (`0x00553a1c`): with room the return address stays pushed and the script carries on at the next instruction; without, it stays parked. A `JSR` that is the body's last word fetches operand 0, pushes the junk word `0xFFFFD8F0` if there is room, and dies.
- `RETURN` (`0x00553a32`) increments first and reads `stack[+0x40]`. With no stack, or nothing pushed (`+0x40 >= stackSize - 1`), it parks (`0x00553a63`), so **a `RETURN` with no frame ends the script**. A popped word without the label tag is dropped through `NOP`: the pop stands and the script carries on after the `RETURN`. The slot is not cleared, so the array keeps stale return addresses; only `+0x40 + 1 .. stackSize - 1` are live frames.
- `PUSH` (`0x00553c1e`) resolves its operand like any value, writes it to `stack[+0x40]` and decrements; on an error it parks. **Either way it writes the value to the result register** (`0x00553c89`, `0x00553cac`).
- `POP` (`0x00553cba`) increments and reads, a return address coming back tagged; on an error it parks and reads 0. Either way it ends in the store tail `0x00555939`: the result register always, then `variables[op0]` only if operand 0 is a variable.

**The heap end** logs *"RSSE: Heap Error"* (`0x765c18`) instead, and an error there **changes nothing** (`0x00553dfa`): no park, no write, the result register untouched.

- `HUSH` (`0x00553d25`) refuses with no stack or `+0x44` outside `0 .. stackSize-1`; otherwise it writes `stack[+0x44]`, **writes the value to the result register** (`0x00553d95`) and increments.
- `HOP` (`0x00553daa`) refuses with no stack or `+0x44 <= 0`; otherwise it decrements, reads, and takes `POP`'s store tail, so a literal destination still pops and still writes the register.

**The engine's call stack is LIFO.** A `Queue<int>` with Enqueue/Dequeue is FIFO and returns a nested call to the *first* caller, not the most recent — that is an inherited bug, not the engine's behaviour.

#### What the shipped content reaches: none of the errors

Measured over all 308 `.RSE` files and the shipped save, and each count re-derived by an independent reviewer with its own parser:

- **`PUSH` and `POP` are never used.** `JSR` 70, every operand a label and none the body's last word; `RETURN` 33; `HUSH` and `HOP` 39 each, in the same 39 scripts.
- **No call error is reachable.** Walked from word 0 with both arms of every conditional and an exact return stack, no `JSR` meets a full or missing stack and no `RETURN` an empty one, in any script. No call nests: the deepest is 1, and every `HUSH` and `HOP` runs at depth 0.
- **No heap error is reachable, and the heap never meets a frame.** Each of the 39 has one `HUSH VAR_LETMEON`, in a boarding loop counted down by `VAR_SPACELEFT`, which is copied from `VAR_CAPACITY` before the loop, with one `ADD VAR_ONRIDE, 1` beside it and no branch between. `VAR_CAPACITY` is written only by `FUN_004dd7f0` (placement with `Upgrades[0].InitCapacity`, `0x004db560`; an upgrade, `0x004dfaf6`; the ride window, `0x004af45b`), which takes a byte and clamps it to the item's `UsageInfo.MinCapacity`/`MaxCapacity` whenever their sum is positive (`0x004dd834`). Every one of the 39 declares its own, and `MaxCapacity` never exceeds the script's stack size (it equals it for jungle `Spider`, 40; hallow `Phantom`, 17; space `tv_ride`, 20); the eight that also `JSR` have room for the call beside a full load. Each unload `HOP`s once per rider counted aboard: straight down `VAR_ONRIDE`, or down a copy of it while `WALKGET` lowers `VAR_ONRIDE`, which holds because `WALKGET` returns only a rider `WALKOFF` let off (`FUN_00557110`). A walk of all 39 with the heap depth tracked, capacity free in `0 .. MaxCapacity` at every read, finds no empty `HOP`, no full `HUSH` and no `HUSH` over a frame, and finds each once the capacity is let past `MaxCapacity`.
- **No branch reads what `HUSH` or `HOP` left in the result register.** Every path from each to the next conditional branch passes an instruction that writes it: an `ADD`, `COPY`, `TEST` or `CMP` whose operand 0 is a variable, a `SUB`, `MULT`, `DIV` or `MOD`, or `WALKGET`. Two unload loops, `bugstv`'s and `Rocket`'s, branch on an `ADD`'s answer across a `WAIT`, which leaves the register alone.
- **The shipped save holds no open frame and no heap value.** In `Easymode.TPWI` the one script with a stack, the Belly Bounce's (3), was saved with `+0x40` 2 and `+0x44` 0; its slot 2 still holds `0x20000018` from a call that had returned, and slots 0 and 1 were never written (`0xCDCDCDCD`). The result register was saved as 3033 (the fountain) and 1 (the gate), 0 for the rest: the fountain's script has no conditional branch, the gate resumes on a `TEST`, which writes it first, and the one script that resumes on a branch, `bus.RSE` at 120 (`BRANCH_Z`), was saved with 0.
- **OpenTPW in the game reaches none of it either** (a throwaway instrumented build; Q83's entry has the runs): in the shipped park and in a bought Inca Totem and Aztec Mayhem, no call was refused, no `RETURN` found no frame, no `HUSH` was refused, no `HOP` found the heap empty and no branch followed a `HUSH` unwritten. The Totem took 15 riders, its heap never deeper than its capacity, 6, and its 17 calls all one deep.

**OpenTPW builds all of this** (`RideScript`, `ParkRides.Resume`; Q83b). `JSR`, `RETURN`, `PUSH`, `POP`, `HUSH` and `HOP` are `Call`, `Return`, `PushCall`, `PopCall`, `PushValue` and `PopValue`, arm for arm, with frames stored tagged `0x20000000`. `ADD` and `COPY` test their destination before the register, and a literal-destination `COPY` leaves the position on its source for the next dispatch to refuse. `WALKON` leaves the register alone, and `GETANIM_CH` with no model stores the register as it stands. A loaded park restores the array, both indices and the register with the counter (`RideScript.RestoreStacks`), taking the saved block's length as the stack's size. The two errors log once per script, where the engine's logger is a bare `RET`.

- *Departures*: `HOP` refuses a heap index past the stack, where the engine reads past the array. No engine writer makes one; only a save could hold it, and neither the save reader nor the restore checks an index. And an instruction whose operands run past the body is not run: `RideScriptFile` refuses the whole file, where the engine runs up to it and there parks (a `JSR` pushing `0xFFFFD8F0` first). No shipped file has one.
- *What shipped content reaches of it*: the error arms stay dead by CONTENT (above), and `GETANIM_CH` with no model is never reached: `ParkRides` hands every bound script its animations and a child inherits its parent's, and in the jungle it ran 30 times, all on the Jungle Spray, each with its model. Reached: the tagged frame, by the Belly Bounce's two `JSR`s to its one subroutine; `HUSH`'s and `WALKON`'s register, masked by the writer before the next branch; and the restore, on every load of `Easymode.TPWI` — the fountain's 3033 and the gate's 1, which nothing reads, and the Belly Bounce's stack with no frame open.

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

**A load puts the scheduler's own state back with the scripts.** `FUN_005597a0` re-initialises these globals (the
flag and the next handle to 1, the rest to nought, `0x00559812`..`0x00559834`) and then reads the module's header
straight over `0x008791a0` (`0x005598d7`): five dwords,
the initialised flag, the tick counter, the next script handle (`DAT_008791a8`, which the loader gives a new script at
`+0x08` and then increments, `0x00558c07`..`0x00558c1c`), the script count and a stale list pointer. The shipped park's
read `1, 6055, 16, 14` and a pointer. Each script keeps its saved handle, so its one-in-eight turn phase carries across
the load, and each is inserted at the head of the list (`0x005599d3`, `0x005599f4`), so a save and load reverses the
order scripts take their turns within a tick. **OpenTPW does the same** (Q180): `ParkRides` restores the tick and the
next handle (`RideScriptScheduler.Restore`) and binds each saved script under its own handle in the module's order, and
the scheduler walks newest first, as `FUN_005516b0` walks from the head. In the shipped park the cameras (handles 8 and
9) leave their saved `WAIT` on ticks 6,136 and 6,137, the first turns after it ends. A thing with no saved script, and
one bought later, is numbered from the next handle. *Departure*: the scheduler walks a copy of its list, so a script
taken down in another's turn gets none that tick, where the engine would follow the dead record's next pointer.

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
| `0x0055a30d` | `FUN_0055a300(frame, value)`, a plain setter, called from **outside the RSSE subsystem**: the object constructor `FUN_004db090` at `0x004db534` finds the script by the id it has just stored at the thing's `+0x24` and pushes the item's own operating speed in, logging `SPEED = %d` |

**No opcode writes it.** A sweep of `0x00551000`-`0x0055a000` (loader, scheduler, dispatcher, every handler) finds **exactly two** writers — the first two rows above; the third sits **past the top of that window**, which is why a scan bounded by the RSSE subsystem misses it. The constructor does the same for a duration through `FUN_0055a0b0(frame, 3, value)`, which writes **variable index 3** — `VAR_DURATION` — and logs `DUR = %d`.

So **"speed is 50 for every script that ever runs" is false**: it is 50 for a script nothing binds, and whatever the item says for a script bound to a placed object. The divisor `0.5 + 0.01 * speed` is neutral only in the first case.

**Which `.sam` key feeds either of them.** The constructor reads its record through an `undefined2 *`, so the `+0xd4` and `+0xd0` the decompiler prints are **element** offsets — byte offsets `0x1a8` and `0x1a0`, the item descriptor's `Upgrades[0].InitSpeed` and `Upgrades[0].InitDuration` by the compiled `.sam` schema (`ride-operation.md`, "What a thing is worth to a guest"). The constructor copies the descriptor's `+0x1a8` into the object's own `+0x58` (`0x004db54c`), the field `FUN_004db7d0` reads a save's `mOperatingSpeed` into, as `ride-operation.md` says of `+0x58`.

**`WAIT <duration>` blocks across ticks without a thread.** First execution: resolve the duration, read the clock (`FUN_00402d70`), compute `now + duration/scale`, store it as a **deadline at `+0xa0`**, **rewind the PC by 2** so the same WAIT runs again, and zero the budget. Later executions take the other path (`+0xa0 != 0`): re-read the clock, and if `now < deadline` keep waiting, else **clear `+0xa0` and fall through** — the PC is already past the operand, so execution simply continues. **An interpreter must model WAIT exactly this way** — as a deadline plus a PC rewind — rather than as a sleep, or scripts will not resume correctly.

**`WAIT` and `WAITANIM` share `+0xa0`, and the engine's "empty" test is NONZERO** — so an interpreter holding the deadline as a plain float with `<= 0` meaning empty reads a past deadline as an empty slot, arms it again every visit, and the script never moves.

The other time opcodes:

    GETTIME  (7)  00551f1e  resolve dest, read clock, store RAW - nothing per-ride, nothing subtracted
    WAITABS (45)  00553828  first visit stores clock + operand, unscaled by speed (0x005538cb), then
                            rewinds and yields like WAIT and shares its compare (0x00553859); ZERO shipped uses
    SETTIMER(95)  0055641b  field[+0xc4] = clock + resolve(operand)  -  NOT speed-scaled, unlike WAIT
    GETTIMER(96)  0055644d  result = field[+0xc4] - clock, floored at 0 by the JNS at 0x0055646d

**`GETTIME` is NOT "how long the ride has been alive"** — that was the published docs' claim and it is wrong; it stores the shared clock verbatim. 172 uses / 57 scripts. **SETTIMER 40 uses and GETTIMER 21, across the same 18 scripts**, and all 21 GETTIMERs name a *literal* destination, so the answer lands in the result register and the write is skipped. **WAITABS has zero uses in all 308**, so it was deliberately left unimplemented.

### What a kept `GETTIME` reading is, and what a load does to it (Q181)

**Every reading a script keeps is a deadline, compared against a fresh reading by its sign.** Across all 308 files (197
distinct bodies, `q181/writers.py`, an independent skeptic sweep agreeing), `GETTIME` names a variable every time, and
only four: `VAR_STARTNOW` (51 bodies, 105 uses), `VAR_TEMP` (54), `VAR_TIMER1` (2: jungle `giftshop`, fantasy `Purse`)
and `VAR_ENDTIME` (1: `End.RSE`, one body in all four themes). Each `GETTIME VAR_STARTNOW` is followed at once by
`ADD VAR_STARTNOW, 10000` (104) or `5000` (jungle `Monkey` word 37); `VAR_TIMER1` by `ADD 4000` or `6000`, except a
1-in-10 arm (`giftshop` 82, `Purse` 65) that stores the raw clock, due at once; `VAR_ENDTIME` by `ADD VAR_ENDTIME,
VAR_TEMP` with 10000 copied in at word 4. Nothing else writes the three. Each is read only as `GETTIME VAR_TEMP; SUB
VAR_TEMP, <deadline>, VAR_TEMP` (`SUB` stores op2 - op3, signed, `0x00551e58`), then `BRANCH_NV` for `VAR_STARTNOW`
(the deadline passed: start the go) and `BRANCH_PV` for the other two (not yet: skip). So `VAR_STARTNOW` is "start
10 s after the last boarder", not a start time. 11 of the 51 guard the test with `TEST VAR_ONRIDE` (each theme's bumper
and go-karts, fantasy `bbugs`, space `rocket`, `zerog`, `zob`); the other 40, `Mumbo` and `Monkey` among them, start an
empty go when it passes. All 54 `GETTIME VAR_TEMP` are followed at once by that `SUB`, so `VAR_TEMP` holds a raw reading
only between those two words.

**A turn can end between them.** The time slice is an instruction budget (`FUN_005516b0`, `0x00551701`..`0x0055173c`),
so an unlocked pair can be split, leaving a raw reading in `VAR_TEMP` and in the result register `+0x48`, which
`GETTIME` writes first (`0x00554b11`) and the save carries. Jungle's unlocked pairs: `bumper` 84, `incagod` 55, `Lookout`
28, `Monkey` 51, `Mumbo` 28, `PorkPie` 28, `Spider` 29, `Totem` 21, `Volcano` 44, `giftshop` 59. `GoKarts` 57, `TourRide`
143 and `Wateride` 78 sit inside `CRIT_LOCK`, and `End.RSE` 51 follows a `WAIT`, whose passing visit starts a turn
(`0x00553817` zeroes the budget on each rewind): those four cannot split.

**The engine's readings survive a load unchanged; OpenTPW's do not.** `FUN_005597a0` reads the variable block raw and
writes no variable after it, and the load puts the `0x785970` clock back to the saved reading (`FUN_00415140` at
`0x00415193`, through `FUN_004031f0`: offset = saved - now), on the load path where `FUN_00414d40`'s fourth argument is
not 1 (`0x004150b5`; only `0x0054f105`, game type 1, pushes 1, and `0x005ad04a` and `0x005ac735` push 2, so the Easymode
load restores it). The clock is integer milliseconds (`FUN_00402f10`, the
`QueryPerformanceCounter` count over frequency/1000, through `__ftol`). OpenTPW copies the variables raw
(`ParkRides.Resume`) but runs its scripts on its own clock (`ParkRides.Moved`), far behind a save's, so `deadline - now`
stays positive until OpenTPW's clock catches the save's: about 80 minutes for Alexah's jungle saves (4.82M ms), 31.8
hours for one taken at the shipped park's clock. The shipped park keeps none. Of the eight holders in Alexah's jungle
`New Save.TPWS` and `autosave.TPWS` (`q174c/clockvars.out`), five write before they read again (`spider` 128,
`gokarts` 197, `tourride` 62/87, `incagod` 159, `porkpie` 125); three read the stale one first: `mumbo` 62 and
`monkey` 85, saved on the branch back into their boarding loops, never start a go by the timeout, empty or not, until
a boarder writes a fresh deadline or the ride fills (the engine starts them 9.5 s and 7.3 s after the load); `giftshop`
59 and 84 never plays its idle `WAITANIM 2`, which nothing but the timer releases.

**Measured in the game** (`q181/run.py`, `run4/`; silent, stock park, a Hot Pot bought at (57,23), one guest admitted
and sent to it, `save/` unchanged), predicted first: with that guest seated in the boarding loop (word 120),
`scriptvar VAR_STARTNOW 114384804` (the shipped park's clock plus the 10,000, what a load of such a save hands over)
held it loading at seated 1 for every reading over 14 s; the control, `0`, put it into its go 2.3 s after the write
(predicted within 2: one reading late). Shots `B1-still-loading` (boats idle) and `A1-going`, looked at. `run3/` shows
the self-repair: a second guest boarded at 20 s, wrote a fresh deadline, and the go began about 13 s later.

**The build moves the readings (Q181b), rather than running the scripts on the save's clock.** Both reproduce every
shipped script; running on the save's clock would widen the float clock that scripts, animation players, the boats,
the people and the lobby share, which cannot hold a saved reading to the millisecond. Moving completes the deviation
`ParkRides.Moved` already makes for every other saved deadline. What a walk can find: a variable written only by
`GETTIME` and `ADD` to itself (`VAR_STARTNOW`, `VAR_TIMER1`, `VAR_ENDTIME`); and, when a saved script stands on a `SUB`
whose word before is `GETTIME V`, that `V` and the result register.

**Built (Q181b):** `RideScript.MoveKeptReadings`, called from `ParkRides.Resume`, moves each by `ParkRides.Moved`. The walk
(`RideScript.KeptReadings`) takes a variable `GETTIME` writes that appears nowhere but as `GETTIME`'s destination, `ADD`'s
first operand or a `SUB`'s operand read: over the 197 bodies it finds `VAR_STARTNOW` in 51, `VAR_TIMER1` in 2,
`VAR_ENDTIME` in 1, and nothing else. A nought is a variable never written and stays. Loading a copy of Alexah's jungle
`New Save.TPWS` moves five (Spider, Mumbo, Go-Karts, Monkey, Pork Pie): the tour ride, Inca god and gift shop are among
the 17 items the Instant Action catalogue leaves out, so nothing binds them (Q186). With the save's `ParkGuestSprites`
crash stepped past in a throwaway build (Q167), Mumbo, one rider aboard and nobody boarding, left its boarding loop 313
ticks after the load (9,526 ms is 307; its turn falls within 8), and without the move was still in it at 401
(`q181b/run3/`, `control1/`).

### `RAND` (28) at `0x00553932` — its bound is NOT resolved

The generator at `0x00516330` is an LCG: `state = state * 0x19660d + 0x3c6ef35f`, rotated right 13, made positive by a `NEG` that hands `0x80000000` back as it is (`0x0051635f`). The opcode then does an **unsigned** `SHR 1` (`0x0055398f`), which turns that one state into `0x40000000`, so no draw is negative; `FINDSCRIPTRAND` halves the same way (`0x005560d0`), and `RideScript.NextDraw` does both, through `ParkGenerator.Draw` (Q176). Then `abs`, `MOVSX ECX,DI`, `INC ECX`, `IDIV`, **takes the remainder**, and `abs` again — so the range is **0 to the bound INCLUSIVE**. **The `MOVSX` is bare**: there is none of the `AND 0xff000000 / CMP 0x40000000` tag test every resolved value operand gets, so a bound tagged as a variable would be read as its own index. All 56 shipped uses name a literal (bounds 1-10 plus one 300 and one 5000), so the difference is invisible in the data and would have been wrong in code. **The state is the world's `mRandomSeed`** (`+0x1da708`, read from the save), and `FUN_00516370` is a plain setter called from eight places, none of them the script system, each of which resets it to an id: a guest's as the guest is made (`0x004fb19e`), at a costume's return (`0x004fe6a1`), at a balloon given and built again (`0x004fe6fc`, `0x0050205f`) and, where a theme has no costume heads, at a head's attach (`0x004fcafd`); a handyman's litter cell's (`0x004d71ca`); and two more (`0x004d94e2`, `0x004f7b7e`). Every draw in the park, `RAND`'s among them, steps that one state, so the original's numbers can be reproduced only by one shared generator started from the save and reset at the same eight points. OpenTPW keeps one per system (`ParkGenerator`), so it reproduces the arithmetic, not the numbers (`ride-operation.md`, "A held balloon").

### The animation opcodes

Opcode numbers: 15 FLUSHANIM, 16 TRIGANIM (3 operands), 17 WAITANIM, 18 LOOPANIM, 19 TRIGWAITANIM, 20 GETANIM, 21 TRIGANIMSPEED, 22-27 the `_CH` variants (one extra operand, the channel), 46 WAIT4ANIM — all funnelling into **`FUN_004732a0`**.

**Every one tests the model handle at `+0xc8` first** — an index into the table at `0x7a4610`, nought when the script has no model. That null path is completely defined. The trigger is `FUN_004732a0(model, animation, parameter, loopFlag, divisor, 0)` returning a length in ms, and `loopFlag` is **0 for `TRIGANIM`, `WAITANIM` and `TRIGWAITANIM`, 1 for `LOOPANIM`** — so **`WAITANIM` really does start the animation**, which the docs page already said.

    FLUSHANIM(15)    00552861  model==0 -> leave; otherwise 00553053 calls FUN_00473270( model, 0 )
    TRIGANIM(16)     00552875  len = trigger(); len -= 300; floor 300 SIGNED (JGE); store in operand 2
                               then 00552fe5: +0xa4 = clock + len/divisor, +0xa8 = 0xffff
    WAITANIM(17)     005529bc  same trigger - but its deadline goes in +0xa0, WAIT's OWN field; the first
                               visit then writes +0xa4 = 0 and +0xa8 = 0xffff (00552b14, 00552b1a),
                               model or not; the re-entry writes neither
    LOOPANIM(18)     00552b2f  key = (op1 << 16) + op0; if key == +0xa8 LEAVE doing nothing;
                               else trigger(loop) if there is a model, then +0xa4 = 0, +0xa8 = key
    TRIGWAITANIM(19) 00552c1a  TRIGANIM's two writes inline (+0xa4 00552d9f, +0xa8 = 0xffff 00552da5),
                               +0xbc = the role plus one, and a rewind of 4 onto itself
    TRIGANIMSPEED(21) 00552e3a role, entry, destination, rate: +0xe4 = the rate (00552f4b), then
                               TRIGANIM's tail; its deadline is len' x 1000 / rate, not divided
    WAIT4ANIM(46)    005538eb  +0xa4 only: nought leaves at once; passed writes +0xa4 = 0 (00553909)
                               and goes on in the same turn; else a rewind of 1 and a yield

**`FLUSHANIM` stops nothing.** `FUN_00473270` writes the sentinel 12 into `channel+0x24` — the role *queued* to play next. The clip actually running is untouched and plays out, and nothing is answered.

**`WAITANIM` is NOT `TRIGANIM` with a wait, and that is the trap.** It stores `len - 300` as the **low half of a qword whose high half is nought** and reads it back with `FILD qword`, so `-300` arrives as 4,294,966,996; `__ftol` (`0x0067a830`) is a `FISTP qword` handing back the **low dword**, so at the divisor 1 nothing overflows and `-300` comes back out; and the 300 floor is then compared with **`JNC`, unsigned**, which a negative passes where `TRIGANIM`'s `JGE` catches it. **So with no model `WAITANIM`'s deadline is `clock - 300`, already past, and the instruction costs exactly one turn** — it still yields, because the engine sets the deadline without looking at it. It does **not** "wait 300 ms". **That holds only at the divisor 1**, the loader's speed 50: the `FDIV` at `0x00552acd` comes between, so with any other divisor a length under 300, or no model at all, makes the quotient's low dword land far from `clock - 300`, ahead or behind by hours to weeks (at the Easymode clock, speed 60 waits about 45 days, speed 49 half a day, and speed 51 passes at once). OpenTPW keeps no divisor (Q155).

`WAITANIM` does trigger the clip: `0x00552a95` tests the model handle and `0x00552ab0` calls `FUN_004732a0`; the no-model path is `XOR EAX,EAX`. **Three of Lost Kingdom's eight items run only `WAITANIM`** — Staff Room, Security Camera, Litter Bin — so a passive wait leaves them inert.

**Two timing asymmetries.** Both `TRIGANIM` and `WAITANIM` divide by the speed divisor (`0x00552acd` is `FDIV float ptr [ESP+0x14]`, the same divisor, worked out afresh per instruction as `0.5 + 0.01 * speed`, exactly 1 at the loader's 50, which a bound script's item speed replaces). The real asymmetry is the **order and the signedness**: `TRIGANIM` floors at 300 **signed** (`JGE`) and *then* divides; `WAITANIM` divides and *then* floors at 300 **unsigned** (`0x00552ad6` `CMP EAX,0x12c` / `JNC`). `LOOPANIM` is idempotence-guarded on the key at `+0xa8` and discards its length. **`0xffff` is what breaks the guard**, and five writers put it there: the loader (`0x00558c4f`, so every new script starts at it), `TRIGANIM` and `TRIGANIMSPEED` (`0x00552feb`), `TRIGWAITANIM`'s first visit (`0x00552da5`) and **`WAITANIM`'s first visit** (`0x00552b1a`). The `_CH` forms never touch `+0xa8`, so a `_CH` trigger leaves the guard standing. A save carries the field (below). No shipped `LOOPANIM` has the key 0 or `0xffff`: all 210 name two literals.

**`WAIT4ANIM` waits on a second deadline field** (`+0xa4`, `0x005538eb`) — **not** `WAIT`'s twin. Two differences, both load-bearing: it never arms anything itself, and **when `+0xa4` is nought it leaves at once without waiting at all**.

**The word at `+0xe4` is the script's play rate, in thousandths.** After every turn of a script with a model, the scheduler writes `(0.5 + 0.01 × speed word) × +0xe4 × 0.001` into every channel's queued speed `+0x30` (`0x00551748`..`0x00551789`, `FUN_00474020` storing at `0x00474037`), which a promoted clip and a loop's replay start at. The loader writes 1000 (`0x00558c33`), and so do eight handlers: `TRIGANIM` `0x0055292e`, `WAITANIM` `0x00552a8c`, `LOOPANIM` `0x00552bd1`, `TRIGWAITANIM` `0x00552d1c` and their four `_CH` forms. `TRIGANIMSPEED` writes its rate operand instead (`0x00552f4b`), and the rate stays until the next trigger: jungle `Gates.RSE`, after its `TRIGANIMSPEED 5, 0, VAR_TEMP, 4000` (word 67), waits in an `ENDSLICE` loop with 4000 there. OpenTPW keeps no `+0xe4` (Q155).

**`TRIGWAITANIM` (19) at `0x552c1a` — OpenTPW reproduces the model path, its raw re-entry included (`RideScript.AnimationOn`), and, as a declared deviation, steps over the model-less one counted rather than parking.** First visit (`+0xbc` is 0): trigger as `TRIGANIM` does, with its two writes carried inline rather than through `TRIGANIM`'s tail (`+0xa4` at `0x00552d9f`, `+0xa8 = 0xffff` at `0x00552da5`, on the model and the model-less path alike), then `0x553693` does `INC EDI` / `+0xbc = EDI`, so the mark is the **animation id PLUS ONE** and 0 means "not armed"; then `+0x3c -= 4` onto itself and return **without** `+0x98 = 0`, so the same turn re-enters it. Re-entry: with a model, `FUN_00473fb0( model, &role, NULL, 0 )` reads channel 0 — a **plain accessor**, not a "channel cursor": at `*(model+0x10) + channel*0x38` it writes `AnimID` (`+0x04`) through its second argument, `SubAnim` (`+0x08`) through its third when that is not null, and returns `Flags` (`+0x00`), with no bounds check and no refresh — then `INC` and compare the **role** against the mark. **The returned `Flags` are thrown away unread** (`MOV EAX,[EBP+0xbc]` at `0x00552d08`), so no pose-flag test lies on any `TRIGWAITANIM` path; among the handlers only `GETANIM` and `GETANIM_CH` test `0x4` (`0x00552e05`, `0x0055374e`). Equal -> `0x5535f4` clears `+0xbc` and falls through; not equal -> `0x5535d6` rewinds 4 **and** sets `+0x98 = 0`. **With no model the accessor call is skipped and the comparison is made against the RAW THIRD OPERAND**, so it parks for ever unless the raw op2 word equals op0 — which **none** of the 133 shipped uses satisfies (132 differ, 1 is a variable). `TRIGWAITANIM_CH` (`0x553494`, its `+0xbc` test at `0x55359b`) shares `+0xbc` and the same tails: `0x553693`, `0x5535d6` and `0x5535f4` sit in its handler, and `TRIGWAITANIM` jumps into them (`0x00552daf`, `0x00552d17`, `0x00552d11`), so the two share one decode rather than agreeing independently.

**`GETANIM_CH`'s operands are `(destination, channel)`**, not `(role, entry)` — **the opposite shape to every other `_CH` instruction.** Counting its first operand as a role inflates any role census. The corrected corpus figures are **627 distinct (item, role) pairs and 806 distinct (item, role, entry) references, all 806 resolving to a shipped file bar the eight known absences**, with the highest entry index any script asks for being 9.

**No opcode hides a mesh of the base model**: ADDOBJ/KILLOBJ/FADEOBJ and the LIMBO family act on the script's own object table, not on model nodes. Variable names like `VAR_LETMEON` and `VAR_LANE1` appear nowhere in the exe — **they are data**. There is no "lane" in the exe; `VAR_LANE1..3` are variables inside the item's own `.RSE` script. `TRIGANIM` uses channel 0 and the `_CH` opcodes take one, so a sideshow's several clips run at once on separate channels.

**Corpus:** `WAITANIM` 547 uses / 250 scripts; `LOOPANIM` 210/114; `TRIGWAITANIM` 133/56; `WAIT4ANIM` 170/75; `TRIGANIM` 74/59; `FLUSHANIM` 15/7. The `_CH` variants are rare (`TRIGANIM_CH` 63/6, `GETANIM_CH` 15/5, `LOOPANIM_CH` 1), `TRIGANIMSPEED` is 4/4 (one `Gates.RSE` per theme), and `GETANIM`, `FLUSHANIM_CH`, `WAITANIM_CH` and `TRIGWAITANIM_CH` are unused.

#### Where OpenTPW's animation state parts from the engine's

Decoded for Q174 by two read-only decoders, each put to a skeptic in Ghidra, and measured over the corpus by two walkers
written apart, which agree site for site: every script walked from word 0 with both arms of every branch and an exact
return stack, carrying the engine's `+0xa4`/`+0xa8` and OpenTPW's side by side. Five differences, and a sixth found by
Q174b's review. **Q174b built the first three and Q174c the fourth**, so OpenTPW now parts from the engine at 5 and 6;
what content reaches each built one is kept, because it is what a change to any of them moves.

**1. `WAITANIM`'s first visit writes `+0xa8 = 0xffff` and `+0xa4 = 0`** (`0x00552b1a`, `0x00552b14`), model or not, and
so does `RideScript.WaitOutAnimation`; the re-entry writes neither. **The `+0xa8` half is reached: 55 `LOOPANIM`s** meet
a key a `WAITANIM` set to `0xffff` after the `LOOPANIM` before it had set the same key, so the loop starts again; without
the write it is skipped and channel 0 stays held on the last frame of the `WAITANIM`'s clip (OpenTPW has no idle
default, and the engine's never fires on a thing's model). 20 of the 55 split on every path that reaches them;
removing the write takes the 55 to nought (one walker's control, and the skeptic's own walk).
**Seven are Lost Kingdom's**, none placed in Easymode: `tvsim` @17, the Aztec Mayhem's `LOOPANIM 2, 0` after the
`WAITANIM 6, 0` @102 that ends every ride cycle; `Wateride` @169 with the ride open (`BUMP 5, 0` answering nought) and
@216 after a breakdown and repair; and `incagod` @32, `Monkey` @32, `Spider` @247 and `Volcano` @193, after a close and
reopen or a breakdown and repair. After those four, the next `TRIGWAITANIM` meets the loop (`Monkey` 131, `incagod` 117
and 142, `Spider` 73 and 101, `Volcano` 91 and 111), so it waits up to a cycle (4166, 9999, 5333 and 3333 ms), where a
held one-shot passes it at once. **The `+0xa4` half is reached at one `WAIT4ANIM`**, space `hoverbot` @259 (armed @209,
cleared by the `WAITANIM` @218), where the older deadline has passed anyway, because both clips queue on channel 0.

**2. `TRIGWAITANIM`'s re-entry reads `AnimID` raw and throws the flags away**, and so does `RideScript.AnimationOn`;
`GETANIM_CH`'s `RoleOn` answers -1 for a held channel. The two part only when channel 0 is held on the triggered role R
at a re-entry: R queued behind another role, promoted, then run out and held before the script's next turn. There the
engine goes on, and a re-entry asking `RoleOn` waits for ever. The hold comes from a frame ending at most 7 ticks
(217 ms) after the promoting one, and OpenTPW runs at most 64 ticks (1984 ms) in a frame, so R's clip must be under about
2200 ms, and under about 280 ms at 30 frames a second. **No shipped content reaches it at a normal frame rate.** The
shortest of the 133 targets is 833 ms (Hallowe'en's `Firework` 5/0). Lost Kingdom has 32 uses in 11 scripts: 15 never
queue (channel 0 idle, held or on R already: the ferry, the seaplane, `Puzzle`, `Squark`, and `Monkey` 215 to 323), and
of the 17 that can, only `Monkey` 202 (`monkeym2`, 1999 ms, queued behind role 4 with 53-300 ms left) is short enough,
after one frame of 58 ticks or more. An entry past a loaded role's count (the engine plays past the table with `AnimID`
R) and a clip of no frames (the engine starts it with `AnimID` R at `0x004730c8`, its length answered as 1000 at
`0x00473425`) still split it, since OpenTPW parks the channel at 12 for both (`AnimTimeControl.Start`); no
`TRIGWAITANIM` names either, and every clip carrying a block declares an end of at least one ("Clip length is the span
the clip declares").

**3. The trigger judges the channel as the last frame's advance left it** (`0x00473315`), and so does
`RideAnimations.Trigger`. A clip that ran out since that advance is busy, so R queues, the next advance promotes it with
its start carried back to the old clip's end, and the same-turn re-entry passes a turn later, 248 ms on; the answered
length and the `+0xa4` deadline count the old clip's remainder as that advance left it (`0x0047337b`). It needs the old
clip's end to fall in the ticks since the last advance (31 ms at a normal frame rate), so it is reached in normal play at
a loop's cycle end, about once in a hundred triggers at the Volcano's 3333 ms: `incagod` 117 and 142, `Monkey` 131,
`Spider` 73, 101 and 218, `Volcano` 91 and 111. With a clip already queued, the engine's queue write replaces it
(`0x0047334e`..`0x0047335e`), as `AnimTimeControl.Queue` does. Only `ParkObjects.Sweep` advances a player, over the models
standing; a player `ParkRides` reads afresh for a thing whose model did not stand is never advanced, so none of its clips
ends, and `ParkRides.PlayersFor` logs one.

**4. A save carries `+0xa0`, `+0xa4`, `+0xa8`, `+0xbc` and `+0xc4`** (`FUN_005597a0` reads the whole struct at
`0x00559a00` and writes none of the five after it; FileFormats `saves.md`), **and each channel's three stamps, speed and
queue** (`FUN_004647a0`, `0x00464bcb`..`0x00464c17`), each a reading of the clock or kept against it; the load makes the
clock read the saved `KOLC` reading again (`FUN_00415140` at `0x00415193`, `FUN_004031f0`), and the first park frame
re-bases the tick loop's `last` to it (`0x0054f425`, on the flag `0x00415189` sets), so the jump runs no catch-up.
**Q174c matched it**: `ParkRides.Resume` and `ParkRides.Restore` put all of it back, each reading moved by its distance
from the saved clock onto the load's moment on OpenTPW's own clock (`ParkRides.Moved`), and `AnimTimeControl.Restamp`
sets the frame as the restore's `FUN_00472cb0` does, the span times 0.03 divided by the speed (`0x00472cf4`, where
every advance multiplies), until the next advance. What content reaches: the Easymode park on every load, the security
cameras' `WAIT 5000` at word 14 with 2,341 and 2,329 ms left, the Belly Bounce's `WAIT 500` at word 46 with 63 and its
key 2 (so its `LOOPANIM 2, 0` @43 does nothing and the saved loop plays on at 1.1), and every running loop's phase (the
Belly Bounce 1,376 ms in, the Fountain 1,625, the Drinks Shop 1,126, the Traffic Lights 220). Alexah's jungle saves add
`WAIT4ANIM` deadlines ahead of the clock (the puzzle's 19.5 s, the ferry's 7.7 s), a `SETTIMER` deadline 284 ms ahead
(the autosave's Aztec Mayhem), and a channel saved with a clip queued (the fantasy save's Caterpillar Capers; the
jungle saves' channel module does not yet walk to its end, Q167). No shipped or played save has a `TRIGWAITANIM` mark set.
What still differs: a new script's own start (the engine's `+0xa8` is `0xffff`, OpenTPW's 0; no shipped `LOOPANIM` has
the key 0); a channel carrying `0x40`, which advances against a second stopwatch (`DAT_007b4974`, `KOLC`'s second dword)
that OpenTPW does not keep, and which no saved channel carries; the real time the engine's clock runs between its
restore and the first tick, through the rest of the load (not measured), which OpenTPW does not count; and the scheduler's tick counter and next handle, which the
module's header puts back (`0x005598d7`), so each script keeps its one-in-eight turn phase across a load where OpenTPW
restarts both (Q180).

**Measured in the game for Q174c** (silent, the stock jungle park loaded under a lobby pause and stepped in frames of
1/60 s, `save/` unchanged; `q174crun.py`, every reading predicted first, 7 of 7 on each build). This build: at the load
the Belly Bounce stood at frame 45.4 of 90 and the Fountain at 48.8 of 50, looping; the Belly Bounce read 55.6 at tick
10 and 41.3 at tick 84, once round at 1.1, with no loop queued behind it in 48 readings over ticks 10 to 60; both cameras
held role 6 at tick 75 and were on role 4 at tick 84, 3.7 and 4.7 frames in. The build before: 4.1 and 3.7 at the load,
the loop queued again behind itself from tick 36, and the cameras on role 6 to tick 84 and role 4 only by tick 176.
Paused photographs of each pair differ in pose where the census says they should (`q174c/*-pair.png`).

**5. A start on an idle channel keeps a stale `0x2` or `0x4` in the engine** (the Flags row above), so its new clip
stands on its last frame (`0x004736bd`), where `AnimTimeControl.Start` clears both on every start and plays it. Two
routes lead there: a trigger naming a role the model lacks over a held or frozen channel, and a save whose idle channel
kept the bit. No Lost Kingdom item lacks a role, and the Easymode save's 163 channels hold 11 held or frozen, none idle.

**6. A clip starts at the tick's own instant in OpenTPW** (`RideScript.StartAnimation` passes the tick's `now`,
`ParkRides.MillisecondsAt`), where the engine stamps a fresh start with the frame's snapshot `DAT_007b496c`
(`FUN_00472bc0`, `0x00472bff`), the one its advance reads (`0x004736b3`). So a clip triggered at tick i of a frame
running k ticks is (k-1-i) × 31 ms in at that frame's sweep here and nought there, up to 1953 ms at the 64-tick cap.
**Kept by decision** (Alexah, 2026-10-01, Q182b): the engine's one instant a frame makes a script's timing hang on the
frame rate, so OpenTPW keeps each tick's own instant 31 ms after the last, said at `ParkRides.MillisecondsAt` and
`RideScript.StartAnimation`. **The cap is kept too**: the engine's loop steps while `now` is past its last stepped time
(`0x0054f4ad`, the lobby `0x0054e77c`), so each tick runs at the start of its 31 ms and a capped frame runs 65 (the
lobby 17); `GameClock` runs one when a whole 31 ms is owed and runs 64 (16). Matching it would move every tick's phase
and the interpolation; said at `GameClock.ParkCatchUp`. Measured (Q182b, `q182b/run2/`, predicted first): a 3 s
`SIGSTOP` stall logged `a 3014 ms backlog capped at 2000 ms, 64 ticks run`, and `state` moved 177 to 241.

**What the engine's ticks read across one frame (Q182).** The park frame reads the clock `0x785970` twice, back to back,
above the catch-up loop: `FUN_00473440` at `0x0054f475` takes the snapshot `DAT_007b496c`, and `0x0054f47f` takes the
loop's `now` (`0x008786bc`). The loop then runs while `now` is past its last stepped time `0x00878c74`, adding 31 to that
(`0x0054f4c4`) and never reading the clock again (back edge `0x0054f8da`); with the backlog clamped to `now - 2000`
(`0x0054f49b`) that is at most 65 ticks, where OpenTPW's clamp runs 64. **Nothing in the loop steps the clock**; the one
thing in it that touches it is a message box opened in a tick, which pauses it (`FUN_004092a0`, `0x004092c8`), so the
frequent case is frozen, not stepped. Its
one stepper, `FUN_00402ef0` (`+0x3c += +0x40`), has one caller chain, `FUN_00409260` ← `FUN_0040f220` ← the scene draw
`0x0054e2f7`, once a drawn frame, and the clock reads `+0x3c` only while latched (`FUN_00402f10`); the park frame
unlatches it each pass (`FUN_00402ed0` at `0x0054f470`) unless `[0x00878128]` is set, which nothing offline sets
(`park-engine.md`, "The game clock"). Latched, the step is `1000 / 0x20` = 31 a drawn frame, so the frames run one tick
each. Unlatched, each read is the raw clock (`QueryPerformanceCounter` scaled to ms, `0x005f5f10`) added to the
accumulator: **across a frame's k ticks the live clock moves only by the real time those ticks take to run**, whatever
k is. The readers inside the ticks (every call of `0x00402d70` whose function the loop's callees reach, with the RSSE
handlers reached by the dispatcher's table): the handlers' deadline reads (`WAIT` at `0x005529a2` and twenty more in
`0x00551f33`..`0x0055645d`), `RSSE_BOUNCE`/`UNBOUNCE`/`FORCEUNBOUNCE`, the walk legs (`FUN_00556f40`, `FUN_005571a0`,
`FUN_00557160`), the every-second-tick scheduler `FUN_00475360` (`0x0047537d`) and `FUN_004758f0` (reached from both sides). The per-frame readers
past the loop: `FUN_00557ab0`/`FUN_00557d80` (the walks' arrivals), `FUN_005580a0` and `FUN_0057ff60`. So in the engine
every tick of a catch-up sees one instant, the frame's `now` plus its own running time, and a script's `WAIT` set in its
first tick cannot come due in a later tick of the same frame; here each tick's instant is 31 ms past the last
(`ParkRides.MillisecondsAt`). How long the original's ticks took to run was not measured: on this computer, up 8 days,
its accumulator holds only multiples of 64 ms (`park-engine.md`, "The park clock loses precision with uptime"), which
is coarser than the quantity asked. Its own per-tick timings are kept by `GetTickCount` (`[0x006fd1d0]`) in eight-slot
shift registers around `Particles_Tick` (`0x008780e8`..), the RSSE tick (`0x008780c8`..) and `FUN_00475360`
(`0x00878108`..), the instrument for a run after a restart.

**How often OpenTPW reaches it** (Q182, silent, the stock jungle park, `save/` unchanged, `q182/run1/`, each reading
predicted first). A frame under 31 ms runs at most one tick, since what is owed after a frame is under one tick; one of
62 ms or more runs at least two. Running, three 240-frame windows at 143.9 fps: worst frames 7.04, 7.05 and 7.13 ms, so
no sampled frame ran two ticks (each window is 1.7 s of the 3 s between readings). Putting down a Hot Pot (its model
loaded the first time and the ground rebuilt under it): one frame of 96.14 ms, so that frame ran three or four ticks,
and a clip triggered in any but the last of them met the difference; 3 s later the worst was 27.71 ms.
So it is reached at a hitch (a first load, a stall), not in steady play at this frame rate.

On the same fields and unbuilt: `TRIGANIMSPEED` arms `+0xa4` and sets `+0xa8`, and OpenTPW counts it, so jungle `Gates`
@80 passes its `WAIT4ANIM` about 0.1 to 0.3 s early and its clip at four times the speed is not played; and every
trigger's deadline divides by the speed divisor (Q155).

**Measured in the game before Q174b** (the build before any change, with a throwaway instrument that keeps the engine's
`+0xa4`/`+0xa8` beside OpenTPW's, seeded from the save, and counts where the two decide apart; silent, the stock jungle
park, each count predicted first, `save/` unchanged). A bought Aztec Mayhem, its queue joined, rode three cycles:
after each cycle's `WAITANIM 6, 0` its `LOOPANIM 2, 0` at word 17 was skipped where the engine's starts the loop, once a
cycle, and channel 0 stood on role 6 held (`0x14`) until the next ride. The loop and the hold look alike on this model,
so its photograph shows the ride and the census the state. The Belly Bounce's saved key split once, at its
`LOOPANIM 2, 0` in its first seconds. No `TRIGWAITANIM` re-entry split, over the ferry (3 first visits, each re-entered
once in the same turn), the seaplane (3, the same), a bought Mammoth Fountain (26 first visits and 60 re-entries, since
its triggers queue) and a Lava Fountain (21 and 48); no first visit took over a channel still busy at the last advance;
no `WAIT4ANIM` split.

**Measured in the game after Q174b** (silent, the stock jungle park loaded under a pause and stepped 3600 and then 7200
frames, so two builds meet at ticks 1939 and 5810; then an Aztec Mayhem bought, its queue laid, `load 40`; each reading
predicted first; `save/` unchanged; a throwaway instrument logging each place the build decides apart from the one
before it). The Aztec Mayhem's script lets its riders walk off after `WAITANIM 6, 0` @102 and then comes round to
`LOOPANIM 2, 0` @17, so channel 0 stands held on role 6 while they leave. The build before stayed there until the next
ride, every census to the end of the run; this one starts the loop again at @17 after each ride, one logged restart a
ride over two rides. In the stock park the instrument logged nothing over both stretches, and the commit's own build
read all 14 things with the same role, entry and hold as the build before at both ticks; the instrumented run differed
at 5810 on the Belly Bounce and the Jungle Spray, both driven by guests whose choices are unseeded, so between runs, not
builds. No `TRIGWAITANIM`, trigger or `WAIT4ANIM` decided apart in either phase.

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

**Corpus:** `ADDOBJ` 644 uses / 164 files, `EVENT` 527/107, `KILLOBJ` 235/122, `FADEOBJ` 113/39, `SETOBJPARAM` 20, **`ADDOBJ_EXT` and `EVENT_EXT` 0 uses**. Node `-1` is the commonest operand in the whole corpus (277 of 644, 341 of 527). Tags run 0-1000 and two tags are killed that no `ADDOBJ` creates. Particle types use ids 1..95; sound types 6..220. **Five shipped requests name an id their category lacks**: jungle
`Speaker2`, `Speaker3` and `Speaker4` run `ADDOBJ 4 -1 6 1` (jungle's `cat_ambient` holds 177-182 and 190-192), and
jungle `incagod` (word 332) and hallow `bumper` (word 188) run `EVENT 3 -1 43` (no theme's `cat_rides` has 43). What
`Sound_PlayEffect`'s category lookup does with a missing id (the manager's vtable `+8`) is not read.

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

### The lights (82-85) — decoded, unshipped

`ENABLELIGHT` `0x00555c7a`, `DISABLELIGHT` `0x00555ccd`, `SETLIGHT` `0x00555d20`, `COLOURLIGHT` `0x00555d9a`. Each
looks operand 1 up with `FUN_0044b220( thingTable[frame +0xc8], 0x20000, node )` (`0x7a4610`) and passes the index
to `FUN_004587b0( thing id, index )` for the light. `ENABLELIGHT` calls `FUN_00458910`, which clears bit `0x10000000`
of the light's `+0x50`; `DISABLELIGHT` calls `FUN_00458940`, which sets it. `SETLIGHT` multiplies its second operand
by the float at `0x00700fe0` (0.01) and calls `FUN_004587e0( light, f )`, which scales the light's base colour by it;
`COLOURLIGHT` calls `FUN_00458890( light, r × 0.01, g × 0.01, b × 0.01 )`, which writes the colour. **None checks for
a missing node**: `FUN_0044b220` answers -1, and `FUN_004587b0` then reads the entry before the table, very likely a
crash. No shipped script uses any of the four.

### `SPARK` (105) at `0x005564ed` — spawns nothing

It stores operands 1 and 2 as 16-bit particle-node ids at frame `+0xdc` and `+0xde`, resolves 3 and 4 and drops them,
and checks each node with mask `0x100` through `FUN_00556b90`, passing the answer and `0x765b58` ("RSSE: Bad particle
node in %s") to `FUN_005da3c0`, a bare `RET` in this build. It calls nothing that spawns or plays. In `0x00550000`-`0x00562000` the only
word accesses at `+0xdc`/`+0xde` are its own four; the nine other hits there are dword accesses in the tour-record
functions (`FUN_0055a620` to `FUN_0055e020`) and `FUN_0055f780`. Outside that range, unsearched. One shipped
use, space `Plasma.RSE`; none in jungle. It stays counted.

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

### The head table — `ADDHEAD` and `DELHEAD`

The script's `+0x30` is an array of `+0x4c` dwords, one per head node of its thing's model: slot *n* holds the visitor
whose head hangs on head node *n* + 1, nought when free. The loader sizes it for every script with a thing and a model
(`FUN_005587f0`, `0x00558d8a`..`0x00558db7`): it calls `FUN_0044b220( thing, 0x80, id )` for id 1 upwards and stops
at the first `-1`, so the count is the run of head-space ids from 1. A load reads the table back from the save, its
count from the block's length (`FUN_005597a0`, `0x00559d3d`..`0x00559da7`; FileFormats `saves.md`).

    ADDHEAD(56)  00554c3e  operand resolved (variable, or the literal sign-extended). +0x30 null -> nothing.
                           no slot holding 0 -> nothing. else draw FUN_00516330 on [0x007cf83c], SHR 1, abs,
                           mod +0x4c, until the slot holds 0 (00554caf..00554ccb): store the visitor, then
                           FUN_0044b220( thing, 0x80, slot + 1 ); -1 -> stop, the visitor kept; else
                           FUN_0044b410( model, node, visitor ) hangs the head. No register write.
    DELHEAD(57)  00554d26  +0x30 null or +0x4c <= 0 -> nothing. every slot equal to the visitor: find the
                           node, FUN_0044b4c0 takes the head down where found, the slot is zeroed. No break,
                           no register write.

**Every count measured** (`q190/heads`, all 67 jungle items): the six that carry the pair have head runs Sun God 32,
Crazy Ape 16, Mumbo 5, Rocky Racers 8, Tom Tom Twister 40 and Eruption 16; the ferry 1, the Inca Totem 13 and the Aztec
Mayhem 27 have runs and no `ADDHEAD`. Alexah's played jungle saves carry a table in seven scripts, each exactly as long
as its model's run (the ferry's and the Aztec Mayhem's empty), five of them holding riders. Five of the six scripts
`HUSH` the rider as the head goes on and `HOP` them before `DELHEAD`; the Sun God puts it on after a walk on and takes it
off after `WALKOFF`. Every head node has a matrix (flags `0xb1` or `0xf1`); Mumbo's and the Crazy Ape's sit on a
morphing face (`0x40040`), the other four's on a clip-driven ancestor.

**Where a head on a morphing face stands.** The pose walk (`FUN_0044ab90`) tests the record's flags against `0x40040`
(`0x0044abf2`) and its parent mesh for runtime `0x200000` (`0x0044ac00`; `FUN_00472d70` sets it on a mesh a clip carries
a morph track for). With both, it reads
the record's face anchor (models.md, "Node lookup ids"): the face's three corners through the mesh's `+0x94` vertex order
(`0x0044ac2a`) from the posed positions at `+0x60`, the face's normal from `+0x64` by the face's first word `& 0x7fff`
(`0x0044ad11`), all through the parent's matrix; `FUN_0044b040` lerps corner 0 toward 1 by `u` (`1 - u` from the `1.0` at `0x006fe2a0`),
that toward 2 by `v`, and adds the normal times the offset; that is the stored translation. With flag `0x40000`,
`FUN_0044a640` also turns the matrix to the face's edge and normal and a kept turn. The morph routine (`FUN_00471860`)
writes `+0x60` and the bounds, not `+0x64`. Measured (`q190/faces`, all 2,073 readable models): every one of the 248
anchors lands at rest on its node's stored place within 0.05 units but the Squark's `Head04`, 0.26.

**After a load the head the save names is hung**: the Sun God's `head08`, which Alexah's save's table holds a rider on,
read runtime flags `0x23` (attached) where its 31 others read `0x21` (`ride-operation.md`, "How long a leg lasts"). No
caller of `FUN_0044b410` is on the load path: the model's record holds the head's sprite slot and the load puts it
back (`saves.md`, "A model record's two tables, and a head on a node").

**OpenTPW builds it** (Q190): `RideScript.AddHead`, `DeleteHead` and `Heads`, the table sized by `RideNodes.HeadCount`
when `ParkRides.NodesFor` reads the model and put back from `SavedScript.Heads` at a load; `ParkGuestSprites.HeadOnRide`
draws each rider in a table as their head on the node and no body, as a bumper boat's (Q179b), **where the node is
drawn this frame** (`LobbyModel.TryGetDrawnNode`, `ModelFile.PointOnFace`): on its tentacle's face as the morph poses it,
or, for a head on a turned arm, where its nearest mesh is drawn. The `rides` census prints
`heads n/slots`, each node's position and the drawn model's node of the same name. *Departures*: a script without
`ADDHEAD` or `DELHEAD` gets no table, which only those two read; the draw is the script's own generator, as `RAND`'s; a
draw that never finds the free slot stops after 65,536 tries and takes the first free one (`ADDHEAD_DRAWS_EXHAUSTED`),
where the engine would hang; a head follows its nearest mesh, where the engine poses every node of the tree, so a clip
turning a node that is not a mesh would not carry its heads; and a head's picture is chosen by its node's turn at rest
(`RIDER_HEAD_TURN_AT_REST`). Confirmed in the game (`q190/run2`, `run3`): a bought Mumbo, 43
census lines with heads equal to riders from 0/5 to 5/5 and back, each head at the drawn node to 0.1; the shots show
heads at the tentacles while it is full and none when it is empty. Then on the tentacles as they move (`q190/follow1`):
in a go each of Mumbo's five heads was drawn on its face and its height moved 5.0 to 8.3 units with its tentacle, the
shot looked at; a Rocky Racers' four followed their cars (`q190/racers1`, 0.4 to 1.6 units), the shot looked at.

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

**What an `EventMap.rse` holds.** Only `COPY` literals into its variables, in **two layouts**, and the engine reads them
**by index**, so one index names different variables in the two (what each slot feeds: `audio.md`, "What an EventMap's
slots feed"). The seven jungle files:

| Ride | Layout | `VAR_EVT0`.. | `VAR_PAR0`.. |
|---|---|---|---|
| `coaster1` | `VAR_EVT0-4`, `VAR_PAR0-4` (10) | 200, 216, 69, 203, 203 | 0, 0, 19, 0, 22 |
| `coaster3`, `minecart` | the same | 204, 216, 69, 203, 203 | 17, 20, 19, 22, 22 |
| `bumper`, `gokarts` | `VAR_EVT0-9`, `VAR_PAR0` (11) | 194, 195, 203, 199, 203 ×6 | 16 |
| `tourride` | the same | 217, 203 ×9 | 21 |
| `wateride` | the same | 200, 203 ×9 | 16 |

**203 is not a sentinel**: the player skips only 0, and 203 is a real effect whose one sample is `Ride:blank.mp2`
(9 ms), so it plays and is heard as nothing. The others in jungle's `cat_rides`: 194 an engine, 195 its stop, 199 a
toot, 200 water, 204 a crumble, 216 the roller start and clicks, 217 wing flaps. 69 is not in jungle's
`cat_rides`, and nothing reads slot 2.

**The eleven names spawned never match their file's case** — scripts ask for `Effects.rse`, `clock.rse`, `worn.rse`, `anims.rse` where the archives hold `effects.RSE`, `Clock.RSE`, `Worn.RSE`, `Anims.RSE` — **so resolution must be case-insensitive or every spawn fails.** All four scripts that spawn inside a loop run `REMOVECHILD` first.

**Corpus:** `SPAWNCHILD` 20/16, `SPAWNSOUND` 28/28, `REMOVECHILD` 4/4, `SETVARINCHILD` 7/3, `GETVARINCHILD` 9/2, `GETVARINPARENT` 10/8, `SETVARINPARENT` **0**, `GETREMOTEVAR` 2 (both in `zob.RSE`, both with a literal destination, so both are test-and-branch), `SETREMOTEVAR` 10/5, `FINDSCRIPTRAND` 5/5.

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
- **`0x2`** (`TEST BL,2` at `0x00559102`), for the script named in the call only, and only when its thing word `+0xac` is set: the item's `Info.DestroyParticleEffect` (descriptor `+0x64`) is spawned by `Particles_Spawn` (`FUN_00521e60`) at the centre of the thing's cell rectangle ×10 at its base height, and its emitter's area is set to the box (`FUN_00466b70`, its heights from the `.hmp`) by `FUN_00520030` — emitter `+0x44/+0x48/+0x4c`, which is the template's `Area`. With no model it spawns at the origin (`0x00559170`). The value is the item's own where it declares one and its category's otherwise (FileFormats `sam.md`, "The particle effects an item gives off" - on the clone's `docs/item-footprints` branch until it merges); 0 spawns nothing (`0x005590f7`), and every value the jungle's items use, 75 to 78, is a world effect (`OnScreen` 0 in `Tp2.plb`).
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

**`INITIALISE` binds the script to its ride.** `FUN_0043b050` walks a linked list from `DAT_00790fe0`, matching `node[+0x24]` against the **script's own id** (field 2, the same id the scheduler uses for `(id ^ tick) & 7`), and returns `node[+0x14]`. That handle is kept at **`+0xe0`** and every other op passes it. With a null handle `INITIALISE` skips its speed push (`0x00554aa5`); ops 1-6 do not test it and index `DAT_00790bd0` with it regardless (`FUN_0043b1f0`, `0x0043b1f4`). It then pushes the script's speed word into the ride via `FUN_0043b2b0`, the same call the execution loop makes each turn.

**`COAST 2 0` is idiomatic, not a bug — and it proves the whole model.** `Coaster1` word 44 is `COAST 2 0` followed at 47 by `BRANCH_Z`. The literal `0` is a **deliberate dummy destination**: the room remaining lands in the result register, the store is silently skipped because the destination is not a variable, and the branch tests the register. So the "ignored write" path is a **feature the scripts rely on**, which is why an interpreter must skip rather than throw.

**Only 12 scripts use `COAST` at all**, and every op count is a multiple of 12 — the coaster family is near-identical across the four themes.

### `BUMP` and `TOUR` — a different object family

Dispatch at `0x005546f5`: `DEC EAX` / `CMP EAX,0x10` / `JA 0x00554c25` / `JMP [EAX*4 + 0x556a18]` — **17 entries, selectors 1..17**, table immediately BEFORE COAST's at `0x556a5c` (over-reading BUMP's table by three entries returns COAST's `00554ac7`/`00554af5`/`00554b39`, a free corroboration of the COAST map).

    sel  1 00554726   sel  5 00554836   sel  9 00554895   sel 13 0055495e
    sel  2 00554766   sel  6 005547f6   sel 10 005548da   sel 14 00554996
    sel  3 00554816   sel  7 005547d6   sel 11 005548fa   sel 15 *** ERROR HANDLER ***
    sel  4 005547b2   sel  8 00554850   sel 12 0055493b   sel 16 005549c8 / sel 17 005549ed

**Selector 15 is invalid in the binary** — its table entry is the error logger itself — **and the corpus confirms it: 199 uses across 12 scripts, selectors 1..14/16/17, and ZERO uses of 15.** A falsifiable prediction from the binary that the data upheld.

**Both prologues make the same call, and only BUMP keeps its answer.** Each reads the 16-bit field at `[EBP+0xac]` and looks an object up through the global at `0x007cf6ec` (`CALL 0x004cd300`); BUMP keeps it in `ESI` (`0x0055470b`), where COAST's operand fetch overwrites it (`0x00554a6f`). BUMP's handlers pass **`[ESI+0x28]`** into functions in `0x00544xxx`-`0x0054axxx`, except sel 5, which reads `[ESI+0x2c]`, and sel 17, which passes `ESI` itself. COAST instead uses the `+0xe0` handle INITIALISE stores, indexing `DAT_00790bd0`, with functions in `0x0043bxxx`. **Two unrelated object families** — so one "ride" serving both would be an abstraction the original does not have.

Sel 5 reads `[ESI+0x2c]` directly with no call; sel 13 and 14 call the **same** function `0x00545100`, one scaling its argument by **30** (30 track ticks of 31 ms, not frames or seconds: "How a bumper ride ends a go", below) and the other negating it; sels 3, 6, 7 and 10 each make one call and return. **EDI is zero on entry to every selector's arm**, so sel 4 passes a literal 0; sels 8 and 9 choose their call by whether the resolved value is non-zero, and sel 17 acts only when the value differs from the word at `+0xe6`, which it then stores. **Two selectors store into operand 1**: sel 2 tests the tag first (`0x00554777`), so a literal takes nobody off and writes nothing, and sel 11 writes the register first (`0x00554913`, `SUB`'s tail), so `BUMP 11 0`, once in each theme's water ride, is a test of the answer. Sels 4, 5, 12, 13, 14 and 16 write the register with no destination, and sel 1 does for a variable operand (`0x00554758`); the others write none.

**COAST and BUMP share one error handler; TOUR does not.** COAST's `JA` (after `CMP EAX,0x7`) and BUMP's `JA` (after `CMP EAX,0x10`) both jump to **`0x00554c25`**, which pushes `0x765bac` = **"RSSE: Unknown bumper ride command"** — so an out-of-range COAST command is reported as a *bumper* fault, logged through `FUN_005da3c0`. TOUR has its own at **`0x005546dc`** pushing `0x765bd0` = "RSSE: Unknown tour ride command", and TOUR's own dispatch is `DEC` / `CMP EAX,0x11` / `JMP [EAX*4 + 0x5569d0]`, so **18 selectors** (6, 7 and 13 are the error path). **`TOUR 1` (`0x00554318`) creates the tour ride**: it finds walk node 99 (`FUN_0044b220`, mask `0x800`), and with it calls `FUN_0055a620( thing +0xac, script +0x08, the node's position ×300, the facing in 4096ths of a turn, operand )`, keeping the answer at frame `+0x9c`; with no node it does nothing (its complaint `0x765bf0` goes to the bare `RET` `FUN_005da3c0`). `FUN_0055a620` takes one of slots 1..99 of `0x8791f8` for a `0x122c`-byte record (-1 when full), the operand at `+0x4c`, and `+0x30` 2500 for a non-zero operand, else 5000. All four shipped uses are `TOUR 1 0`. **`TOUR 2` (`0x0055441b`) destroys it** through `FUN_0055d3d0`, which the script's teardown `FUN_00558500` also calls (`0x00558536`), and fetches no operand; no shipped script uses it. **Never write an engine string from memory or by analogy** — one was invented and caught only by reading `0x765bac`.

**`ScriptDefs.Bumper` is NOT a selector table — do not implement from it.** Its values are -1, 0, 7, 32, 38, 47, 54, 93, 115, 121, 134 and **18770**, and the dispatcher accepts only 1..17, so they cannot be selectors at all. `ScriptDefs.Coaster` matched COAST's table exactly, which makes the mismatch here easy to miss by analogy. Whatever those numbers are, they are not what `BUMP` switches on.

### How a bumper ride ends a go, and lets its riders off

Decoded for the Hot Pot (`QUEUE.md` Q179: the fork review's gap1 items, read again first-hand and by three skeptics,
`wf_37cc14a6-a0a`, eleven claims upheld with details corrected). Aluzed's OpenTPW-decomp fork pointed at the selectors
first (its T-007 item 22 and `FUN_0054a040`); the unload chain is found here.
**`BUMP` works on the track-ride record, and the engine's track tick ends the go, not the script.** The object's
`[+0x28]` is a handle `slot | BumperType << 8`; the record is `DAT_00877b60 + (h & 0xff) * 0xd0`, and every callee
checks the `GFEJ` magic (`DAT_00877b58`) and the handle before touching it. Templates, one per BumperType in order from
-1, are at `0x764178` (the Hot Pot's: duration 4350, car radius, mesh base 1 count 1, most cars 8).

| Record | | Car (pool `DAT_00877b68`, 256 of `0xac`) | |
|---|---|---|---|
| `+0x00` | BumperType | `+0x00` | flags: 1 live, `0x20` unloading, `0x4000` active, `0x100000` new, `0x400000` lead |
| `+0x04` | duration, in track ticks (`BUMP 13`, `14`) | `+0x08` | model |
| `+0x1c` | performance 0-100, set at open (`FUN_00545180`) | `+0x24`, `+0x28` | emitter nodes (mask `0x100`) |
| `+0x50` | state: 0 closed, 1 loading, 2 running | `+0x30` | rider list |
| `+0x54` | 0 sound, 1 worn (`BUMP 9`), 2 broken (`BUMP 8`) | `+0x88` | timer |
| `+0x58`, `+0x5c` | lead car; cars on the ride (`BUMP 11`) | `+0x9c` | its record |
| `+0x60`, `+0x64` | riders seated; most cars | | |
| `+0xc4`, `+0xc8` | boarding list; leaving list | | |

A list node comes from `DAT_00877b8c`: `+0` peep, `+4` handle, `+8` seat node (-1 none), `+0xc` seat id, `+0x10` next.

**What each selector calls** (arms at the table above; every one checks the handle first):

| Sel | Callee | What it does |
|---|---|---|
| 1 | `FUN_0054aa80` | With the ride not closed and a free node, pushes the peep onto the head of the boarding list; 1 or 0 in the register. A literal operand skips the call and the write (`0x0055473d`). |
| 2 | `FUN_0054ab40` | Pops the head of the leaving list and answers the peep or 0, into the variable and then the register. |
| 3 | `FUN_00544f90` | Only in state 1, and not for the water family or -2: state 2, and every car of the ride timed to `+0x04` and retargeted; a Hot Pot car still flagged `0x4000` to anim 5. "Start Bump Ride". |
| 4 | `FUN_00549db0( h, 0 )` | Not closed, and `+0x5c` below both `+0x64` and 64: launches a car from the pool, timed to `+0x04`; it takes the whole boarding list and seats it; the car or 0 in the register. |
| 5 | none | Answers the object's `+0x2c`, `mIsTrackRideValid`. |
| 6 | `FUN_00544a10` | Unless closed: state 0, the boarding list onto the leaving list's tail; bumper cars timed 0 and set `0x20`. "Close ride". |
| 7 | `FUN_00544840` | Types -1, -2, -3, -6, -11 and -14: state 1, whatever it was. The karts and water, only from 0 (1 or 2, and the start buoy). Then the performance. "Open ride". |
| 8 | `FUN_00544c80` / `FUN_00544e50` | Non-zero: `+0x54` = 2, smoke at each car's emitter, Hot Pot cars anim `0xc`. Zero: `+0x54` = 0, and only from 2 "Ride Fixed", smoke killed, anim 5. |
| 9 | `FUN_00544c00` / `FUN_00544e50` | Non-zero: `+0x54` = 1, "Ride worn out". Zero: as 8's. |
| 10 | `FUN_00544b50` | Removes every car (their riders to the leaving list), then closes. |
| 11 | `FUN_005452a0` | Answers `+0x5c`. |
| 12 | `FUN_00549b80` | Not closed: the first car of the ride with no riders takes the whole boarding list, is timed and seated; 1 or 0. It does not skip a car still unloading, and retargeting it clears `0x20`. With no car empty the list stays for the next `BUMP 12` or `4`. |
| 13 | `FUN_00545100( h, v * 30 )` | The duration; the register gets `v` unscaled. |
| 14 | `FUN_00545100( h, -v )` | Laps, stored negative; the register gets `v`. |
| 16 | `FUN_0054ad90( h, 1 )` | The operand is fetched and ignored. Removes the first empty car and answers 1; with none, removes the first car whatever it carries and answers **0**. |
| 17 | `FUN_0052a490`, `FUN_0052a700` | Only when `v` differs from the script's `+0xe6`, which it stores: the ride's track pieces to anim 5 (`v` 0) or `0xc`. |

**The chain, for the Hot Pot's `bumper.RSE`.** `BUMP 7` @12 opens it; `BUMP 4` @55 launches cars up to
`VAR_CAPACITY`. Each admission is `BUMP 1 VAR_LETMEON` @102, then `BUMP 12` @108, which puts the boarding list into
the first empty car (a `b_car` has one seat, `0x80` id 1; `FUN_00549c60` seats a rider and adds to `+0x60`). With
the capacity filled or the wait over, `BUMP 13 VAR_DURATION` @125 sets `+0x04` = `VAR_DURATION` × 30, `WAIT 1000`,
and `BUMP 3` @136 starts the go. From then on **the track tick `FUN_00546c80`, once per 31 ms tick, calls
`FUN_005474b0` for each live car** (and `FUN_00547f50`, the step, if the car is still live), which counts the car's timer down while it is at least 0, the state is 2 and the
ride is not broken (`+0x54` 2 freezes it), and at 0 sets `0x20` (the bumper family also clears `0x4000`). **A timer
started at 0 goes to -1 and never counts again**; only `BUMP 6` unloads such a car. With `0x20` set, each tick the bumper arm calls `FUN_0054ac70( car, 1 )`:
every rider on the car goes to the head of the leaving list (so the order reverses), `+0x60` goes down by one each ("Peep %d added to ride %d
leaving list", particle `0x13` where a rider was seated), and it answers `+0x60`, **the ride-wide seated count**: only
when that is 0 does the state go back to 1 and the car to anim `0xc`; a car handled while another still holds riders
tries again next tick. Its looped sound is faded on every pass. The script's `BUMP 2 VAR_LETMEOFF` @221 then
takes one rider a pass and loops on `TEST VAR_LETMEOFF` until the engine has dismissed them (`ride-operation.md`,
"`VAR_LETMEOFF`"), counting `VAR_ONRIDE` down. So a Hot Pot go lasts `VAR_DURATION` × 30 ticks of play not broken after `BUMP 3` (the
script runs after the track tick in a step, so the count starts on the next): the bought one's duration 25 gives 750
ticks, 23.25 s. The template's 4350 is overwritten by `BUMP 13` before any car is timed.

**When the tick runs.** `Game_StateMachine`'s park case calls `FUN_00546c80` at `0x0054f55f` once per step of its
catch-up loop, each step 31 ms of the park clock, before the every-eighth-step gate and the three-a-frame cap: none
or many in a frame, the backlog held to 2000 ms. A pause stops it by freezing the clock; an inactive full-screen
window drops the step.

**Other writers of a car's timer and `0x20`.** `BUMP 3`, `4`, `6` and `12` (above); the buoy arm's increment at
`0x00547aae`, which needs a buoy flagged 4, and the bumper family's eight buoys (`FUN_00545890`) never are; and **the
save loader `FUN_00543560`, chunk 5, which restores whole car records**, flags and timer included.

**Three quirks to copy, not fix.** `BUMP 6` flags every bumper car to unload, so on the next tick a closed ride with
cars reads state 1 again (the boarding list it moved was never counted in `+0x60`; a ride with no cars stays 0). `BUMP 16` answers 0 after removing an occupied car, so the script's `BUMP 16 0` /
`BRANCH_Z @65` / `ADD VAR_CARS -1` (@45) leaves `VAR_CARS` one above `+0x5c`. And removal (`FUN_0054ae50`) that
empties the ride sets state 1, "Ride Over - reset to loading", except for the water family.

**What the bumper family's chain does not need.** The cars' motion (`FUN_0054a040` picks each car's next buoy,
`DAT_00877b78`, or another car to chase; `FUN_00547f50` steps it) and their sounds and emitters are apart from the
unload: nothing in the chain above reads a car's position. Go-karts and the water ride end their cars by reaching
buoys instead (`FUN_005474b0`'s buoy arm, "GoKart Race Over"), so building them needs the steering.

### Where a bumper ride's cars float

Read for Q179b (the build of the section above), first-hand in Ghidra. **A car is launched where its arena is,** by
`FUN_00549db0`: the first pool car not flagged live, zeroed, flagged live and `0x4000`, its record at `+0x9c`, its
timer the duration (`+0x04`), its riders the whole boarding list, its mesh `+0x04` = cars (counted with it) mod the
template's `+0x18`, plus `+0x14`, an index into the item's `SupplementalMeshes` (the item's loaded handles at `+0x4bc`, which `FUN_00413c10` fills at `0x0041434f`..`0x0041438b`, loading each name from `+0x244` on, the start taken from `ride-operation.md`'s `SupplementalMeshes[7]` at `+0x260`; whether the go-karts launch here, and not through the other readers of `+0x4bc` at `0x00543d00`, `0x0055aa00` and `0x0055e438`, is not traced), its model playing role `0xc` (the
"no animation" sentinel, `RideAnimations.NoRole`) with flags 2, so it stands still; the first car of a ride is its lead
(`+0x58`, flag `0x400000`). It is seated (`FUN_00549c60`), retargeted (`FUN_0054a040`) and flagged `0x4000000`, which
the draw clears as it spawns particle `0xf`, a splash.

**Where in the arena.** `FUN_0054a040`'s bumper arm (types -1, -3, -6, -11, -14), for a car still flagged `0x100000`:
up to 100 tries of a radius `rand % arena` and an angle `rand & 0x1ff`, the point `x = centre + cos·r >> 8`,
`z = centre - sin·r >> 8`, kept on the 100th try or the first clear of every other live car by their two radii
(`+0x64`); then `0x4000` cleared, a heading `+0x50`/`+0x54` = `rand & 0x1ff`, the emitters looked up (`0x100` ids 2 and
1), and for the Hot Pot alone flags `0x3000000` and a second model, supplemental mesh 0 (`b_wake.md2`). Its next
target, a buoy or (3 in 16, with two cars or more) another car, is the motion's ("How a bumper ride's cars move", below). **The arena** is the
collision object `FUN_00545890` lays at `+0xc0`: centre `+0x74`, `+0x78` = the placer's x and z plus `0x600`, radius
`0x1200` for -1, `0x1100` for -11 and `0x1600` for -3, -6 and -14; its eight buoys ring it at `0xc00`. **The placer**
(`FUN_00529e10`) passes x and z = (cell + `Bumper.<turn>XAdjust`/`YAdjust` + its own offset) × 3072, the offsets
(4, 1), (1, -5), (-5, -2) and (-2, 4) for turns 0, 90, 180 and 270: for the Hot Pot's 5 × 5 the centre is the middle
cell's centre at every turn, where its model's pot stands, (25, 25).

**The Hot Pot's template** (`0x764178`): BumperType -1, duration 4350, car radius 768, mesh base 1 of 1, performance 50,
most cars 8. **The pools** (`FUN_00544360`): 64 records of `0xd0`, 256 cars of `0xac`, and 1024 list nodes of `0x14`;
a node is taken by `BUMP 1` and given back by `BUMP 2`, and a ride let go of (`FUN_00545610`, which removes every car
and closes the ride first) keeps whatever its lists held. **The sine table** `DAT_00877358`: 512 steps,
`ftol( sin( 2k × c ) × 256 )`, where `c` (the double at `0x700ec8`) is 0.00613591796875, just under π/512, so steps
128 and 384 truncate to 255 and -255, not 256. Products of it are divided by 256 toward nought.

**What rocks.** `BUMP 12` flags the car it fills `0x84000`, so `BUMP 3` turns only the cars `BUMP 12` filled to role 5
(`b_carm`, looped, flags 1); an empty car stays at rest through the go, and every car is put back at rest when the
ride is empty again (a car launched by `BUMP 4` with the boarding list aboard has had `0x4000` cleared, so it stays at
rest too; the Hot Pot's script launches before it admits anyone). Each track tick counts a car's `+0x90` up
(`FUN_005474b0`). **Every retarget** ends at the target step, which clears `0x20`, `0x100000`, `0x40000` and bits
`0x52` whichever target it takes, so `BUMP 12` refilling a car still flagged to unload keeps its new rider.

**The draw** (`FUN_00546280`, once a frame a ride): x and z × 1/307.2 (`DAT_00700edc`), so a cell is 10 units; the
four corners at the car's radius along its heading each take a height from the scene under them (`FUN_00450ac0`,
`FUN_00450ea0`, `FUN_004511a0`, not decoded), plus, for flag `0x1000000`, a bob of 0.00125 (`DAT_00700ef0`) × the sine
of `+0x90` × 6, 4, 3 and 5, eased across the frame; the car stands at their average, pitched and rolled by their
differences, turned `((heading - 0x100) & 0x1ff)` 512ths of a turn; its wake (flag `0x2000000`) trails it by its speed,
0.7 higher. **A rider is attached to the car's seat node**, found as `0x80` and its seat id (`FUN_0044b220`) and
attached by `FUN_0044b410`: the `b_car`'s one
is `Head1` (id 1, flags `0x100000b1`); a second rider of a car has no node and is still counted seated.

### How a bumper ride's cars move

Read for Q179c, first-hand in Ghidra (the functions are named there, `Bumper_*` and `TrackRides_Tick`), checked by three
skeptics (`wf_ab4da4ce-b87`) and against the original's memory in a go (below). Units are the placer's: 3072 to a cell,
a heading in 512ths of a turn whose direction is (sin, cos) in (x, z), the sine table above, every product of it
divided toward nought per axis. A heading from an offset is `ftol( 256 − atan2( dx, −dz ) × c × 256 )`, `c` the double
at `0x700eb8` (0.3183...); `ftol` truncates. A distance (`Bumper_Distance`, `FUN_00549020`) is `ftol( sqrt )` of the
offsets halved together until x is at most `0x8000` and z at most `0x7fff`, shifted back.

**Each 31 ms track tick** (`TrackRides_Tick`, `FUN_00546c80`) makes two passes over every live car in the pool. The
first, for each car: clear `0x200000`, `Bumper_CarTick` (`FUN_005474b0`: the timer above, then the target and the
thrust), then, if still live, `Bumper_StepCar` (`FUN_00547f50`). The second, for each car: if its collision object
lacks `0x80` and it lacks `0x200`, the bump against **every** other such car in the pool; then, for every car,
`Bumper_KeepInObject` (`FUN_005497b0`), `Bumper_PushOffObstacles` (`FUN_005494d0`) and `Bumper_PlayCarSound`
(`FUN_00547170`). The second pass reads none of the ride's state (the first does: `Bumper_CarTick` and `Bumper_StepCar` test its `+0x50`
and `+0x54`), so empty boats are pushed about and kept in the pot
while the ride loads.

| Car | | Record (`Bumper_SetPerformance`, `FUN_00545180`) | |
|---|---|---|---|
| `+0x34`, `+0x38` | x, z | `+0x20` | thrust, lerp of `+0x30` to `+0x40` |
| `+0x3c`, `+0x40` | velocity | `+0x24` | friction, of `+0x34` to `+0x44`, in 1024ths |
| `+0x44`, `+0x48` | the velocity after this tick's friction, before any bump | `+0x28` | turn, of `+0x38` to `+0x48`, 512ths a tick |
| `+0x4c` | speed, `ftol( sqrt( vx² + vz² ) )` | `+0x2c` | restitution, of `+0x3c` to `+0x4c`, in 1024ths |
| `+0x50` | steering heading | `+0x68` | arrival radius, 1024 for all four |
| `+0x54`, `+0x58`, `+0x5c` | drawn heading; the heading it turns to; its turn this tick | `+0x90`, `+0x94` | the arena centre again |
| `+0x64`, `+0x68` | radius (template `+0x08`); thrust bonus, 0 (launch zeroes it; only the karts' and water's arms write it) | `+0xa8`, `+0xac` | first buoy; buoys |
| `+0x6c`, `+0x70` | the point steered at | | |
| `+0x74`, `+0x78` | its offset from the target (x, z) | | |
| `+0x7c`, `+0x80` | buoy slot; car chased (its pool index) | | |
| `+0x8c` | patience | | |
| `+0x98` | the collision object it is in | | |

**The performance** is `clamp( p, 0, 100 )` at `+0x1c`, each of the four `lo + ( hi − lo ) × p / 100`. The placer lays
it from the template (50); `BUMP 7` sets it again from `+0x1c`; the save loader from the saved dword (`0x00543788`);
and **each scheduler visit of the ride's script pushes the script's speed word** (`0x00551844`, `FUN_005516b0`, through
the thing's `+0x28`), whether or not a turn ran, logging "Set ride performance to %d". The word is the ride's operating
speed, pushed by the constructor and an upgrade only when `InitSpeed` is over nought (else the loader's 50): 60 for
all four bumper rides as bought, none of which sets `Upgrades[0].InitSpeed` over `Rides.sam`'s 60.

| BumperType | ride | radius | thrust | friction | turn | restitution | at 60 | arena, buoys |
|---|---|---|---|---|---|---|---|---|
| -1 | jungle Hot Pot | 768 | 8-12 | 960-1010 | 8-14 | 1000-1100 | 10, 990, 11, 1060 | `0x1200`, ring `0xc00` |
| -6, -14 | hallow and space `bumper` | 896 | 10-16 | 940-1000 | 6-12 | 1000-1100 | 13, 976, 9, 1060 | `0x1600`, ring `0x1000` |
| -11 | fantasy `bbugs` | 896 | 10-16 | 940-1000 | 6-12 | 1000-1100 | 13, 976, 9, 1060 | `0x1100`, ring `0xb00` |

(-3, which no item names, has duration 4350, radius 768 and -6's ranges and arena.) The ring is eight buoys, the
first at the centre's (0, +ring), each next the last turned by `( x, z ) → ( (181x + 181z) / 256, (181z − 181x) / 256 )`,
so the Hot Pot's are (0, 3072), (2172, 2172), (3071, 0), (2171, −2171), (0, −3069), (−2169, −2169), (−3067, 0),
(−2168, 2168).

**The next target** (`Bumper_Retarget`, `FUN_0054a040`, bumper arm, after the placement above). Unless the car holds
`4` or `0x40000`, one draw of the park's generator (`FUN_00516330`): with its low four bits under 3, and the ride's
`+0x5c` at least 2, it **chases a car**, chosen by a second draw `%` the count from the live cars of the same ride in
a joined object, not itself, not flagged `0x20` (empty ones too); its pool index to `+0x80`, patience 90, flags
`| 0x8004` with `2`, `8`, `0x10`, `0x20`, `0x40`, `0x40000` and `0x100000` cleared. Otherwise **a buoy**: if the car
holds `8` (it was at one), a draw with its low four bits under 4 takes the next buoy after its own; else a draw `%`
the ring's count steps on from the first. Flags `| 0x18008` with `2`, `4`, `0x10`, `0x20`, `0x40`, `0x40000` and
`0x100000` cleared, patience 3. The lead's looped sound starts here when it has none, is flagged `0x4000`, is not
unloading and the ride is running: at the go's own retarget (`BUMP 3`), never at placement or a fill, which find the
ride loading (measured: the go's first tick; `audio.md`, "The bumper arm's engine").

**What a car steers at** (`Bumper_CarTick`).
- **A car** (flag `4`, tested first): patience goes down every tick; at nought, or with the chased car not live or
  flagged `0x20` or `0x40000`, retarget. Otherwise lead it: `t = min( distance / max( own speed, 1 ), 24 )`, its
  own speed taken afresh from `+0x3c`; aim at the chased car's position plus its velocity × `t`; if that point is in
  neither the chased car's object nor any other not flagged `0x40`, pull it inside the nearest joined one
  (`Bumper_NearestJoinedObject`, `FUN_00549070`): the centre plus `(offset / 8) × radius / ( distance(offset / 8) + 32 )`.
  `| 0x10000` every tick, so the point (plus the car's last offset) is taken again each time.
- **A buoy** (flag `8`, `0x20` clear): `Bumper_SteerToward` the buoy. If the car holds both `0x8000` (every target
  sets it, only a launch clears it) and `0x40000` (bumped), retarget. Within the arrival radius of the point steered at
  (`<=`), patience goes down by one: at nought, retarget; otherwise take the ring's next buoy, the old x offset
  becoming the z offset and the x offset `512 − draw % 1024`, and `| 0x10000`. Outside it the point stays fixed. So a
  car rounds two more buoys after the one it chose, then chooses again.

**The steering** (`Bumper_SteerToward`, `FUN_00547c60`). With `0x10000` and an object: if the bare target lies in an
object joined to the car's (the same object for a bumper ride), the point steered at is the target plus the car's
offset, and `0x10000` is cleared; if it lies in no object, the bare target is stored and the function answers −1 at
once, no turn or thrust, which the caller reads as an arrival (unreachable here). Then the bearing
`b = ( heading_to_point − +0x50 ) & 0x1ff`, folded to 0-256 with its side kept. **Turn**: if `b` is over half the
record's turn (toward nought), `+0x50` moves the whole turn toward it, so it can overshoot; this needs `0x4000` and
the ride not broken (`+0x54` 2). **Thrust**: if `b` is under `clamp( distance >> 9, 22, 48 )` (22 anywhere in the Hot
Pot's pot), velocity `+= ( thrust + bonus ) × ( sin, cos )` of `+0x50`, three quarters of it in an object not whole
(`& 0x1e` not `0x1e`); this needs `0x4000` without `0x80000`, and the ride not broken. `0x4000` is cleared at placement
and at the end of the car's go, and set only by `BUMP 12`, with `0x80000`, which `BUMP 3` clears: **only a car with
riders, in a go, drives**; a filled boat waiting for its go swings `+0x50` but does not move, and an empty boat only
drifts where it is pushed.

**The step** (`Bumper_StepCar`). Friction `k` is the record's; `k × 7 / 10` for a car flagged `0x80000` (tested
first), else 1000 for a broken ride. Velocity `= v × k / 1024`, copied to `+0x44`; speed taken; position
`+= velocity`. Then the heading, only while running (else `+0x5c` = 0): **the Hot Pot alone** turns its drawn heading
toward the steering heading, `+0x58 = +0x50`, `+0x5c = wrap( +0x50 − +0x54 ) × speed / 1000`; every other bumper type
turns toward its velocity's heading at the same rate. `+0x54 = ( +0x54 + +0x5c ) & 0x1ff`. Thrust is at most 9 an axis
(the table peaks at 255), so a Hot Pot boat's steady speed is at most 247 a tick (232 along an axis), 2.4 to 2.6
cells a second. Each tick it also moves the lead's looped sound to the car and sets the parameter the `EventMap`'s slot 10 names to
speed / 3, which pitches it (`audio.md`, "A voice's two controllers"), and the emitter `+0x2c` to
node `+0x24`; for types other than -1, every 32nd tick in a go, a draw of the private generator at `0x00877b90`
(`× 214013 + 2531011`, which the Hot Pot draws too) spawns particle 1 one time in four. The draw (`FUN_00546280`)
eases the car across the tick by its velocity and `+0x5c`, scaled by the draw's argument.

**A bump** (in `TrackRides_Tick`, car A the outer loop's, B the inner's). With `0 < d < rA + rB`: `h` the heading
from A to B, `u` the heading of A's `+0x44` less B's, minus `h`. **Closing** (`u` under 128 or over 384):
`j = ftol |vA − vB| × cos u` (of the `+0x44` pair), `s = j × e / 1024` with B's restitution `e`; B's velocity
`+= s × ( sin, cos ) h` and B `| 0x50000`; A's `−= ( j − s ) × ( sin, cos ) h`. No bump writes `+0x44`, so the pair's
second meeting, from B's side, kicks again from the same snapshot with the roles swapped. At a restitution of 1060,
`j − s` is negative once `j` is 29 or more, and A is pulled after B. **Not closing**: overlap
`o = offset − offset × ( rA + rB ) / d` on each axis, B moved by `−o × e / 1024`, A by `o − o × e / 1024`; positions
only. **The bump's sound** is asked only in the closing branch, when `s` is over 130 and neither car holds
`0x200000` (then both do): code 3 ("CRUNCH") for the karts, 5 for the -3, -6, -11 and -14 arenas. The Hot Pot's
boats bump silently.

**Kept in the pot** (`Bumper_KeepInObject`). A car whose position leaves its object takes the first other object not
flagged `0x40` that holds it, `| 0x10000`; in none, it is put on the rim: velocity (with this tick's bumps)
`× e / 1024`, then turned by `2 × ( h_out − h_v ) − 256`, `h_out` the heading from the centre and `h_v` that of
`+0x44`, which reflects it; position `centre + offset × radius / d`. `+0xa4`, the stuck count, is −1 for every bumper
car, so it never counts. `Bumper_PushOffObstacles` acts on objects flagged `0x40`, which only the unused type −2 lays.

**Measured in the original** (Q179c, `q179c/boatlog.py`, the reference park patched to research the Hot Pot, bought at
speed 60 and capacity 4; its clock ran 1.56 times real time at 8 days' uptime, so only ticks were counted, not
seconds). The record read performance 60, thrust 10, friction 990, turn 11, restitution 1060, as predicted. Two goes
lasted 750 ticks each. The decoded step (turn, thrust, friction, position, with no other car within 2000 and the rim
600 away) reproduced 2308 of 2323 logged transitions exactly; the other 15 fall on three pairs of adjacent ticks, read
while the game was writing them. Top speed 228; no boat past the rim; all eight buoys targeted; chases seen. In the
first go two boats held riders: they drove in long curves through the buoys, and the two empty boats moved only when
struck. Filled boats waiting for the go swung `+0x50` and stood still. Photographed (`q179b/orig/go-*.png`) and plotted
(`q179c/go1-paths.png`).

**Differences a build must keep.** Every draw that steers is the park's generator, shared with everything else, so no
two runs match boat for boat; a build matches the rules and the numbers, not the paths.

**Read for the build (Q179d), first-hand.** The arena is laid flags `0x1f` (live, all four quadrants, round), so it is
whole and the three-quarter thrust is never taken; `Bumper_PointInObject` (`FUN_005493f0`) holds a point on its centre
or at a distance (halved as above) no more than its radius. A bumper ring's buoys are flagged 1 alone and hold no object
(`+0x18` 0), so the arrival takes the next buoy without the object check. `Bumper_LaunchCar` (`FUN_00549db0`) zeroes the
car, sets `+0x7c` and `+0x80` to −1, and puts at `+0x98` the first object that holds the arena's centre. The chase list
is built by pushing each candidate on its head in pool order, so the draw counts back from the last. **The draw eases
back, not ahead**: `Game_StateMachine` passes `FUN_00519060` (and so `FUN_00546280`) `(DAT_008786bc − DAT_00878c74) ×
1/31` (`0x0054fa0d`..`0x0054fa33`), the clock less the last stepped time, which the catch-up loop has carried past it
(`0x0054f4c4`), so between −1 and 0; a car is drawn at its position plus its velocity times that, its heading plus
`+0x5c` times it, and each corner's bob `s[p×m] + (s[p×m] − s[p×m − m]) ×` it. OpenTPW's own per-tick census, replayed
through Q179c's step check, matched 285 transitions of 285 (`q179d/run4/`).

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

**And that main loop contains no `ENDSLICE`.** The only reason a turn ever ends is the `CRIT_UNLOCK` at word 62 zeroing the budget — and a machine that misses that semantic still ends it about four laps in, once the unlocked words have spent the budget, so the miss changes how much a turn does, not whether it ends. `Coaster1` also declares **no stack at all** (`#setstack` 0), so it exercises nothing of `JSR`/`RETURN`. **The useful shape for a test is to *find* a script that reaches a `WAIT` by running them all, not to name one.**

**A lock reached on the last unit of the budget**, the one arrival where charging `CRIT_LOCK` would end the turn inside its section. Walked with every branch arm possible from every turn start (the entry, after each `ENDSLICE` and `CRIT_UNLOCK`, each waiting instruction, and wherever a budget of 50 runs out, to a fixpoint), **68 of the 150 locks can be dispatched after exactly 49 costed instructions, 18 of the 36 in Lost Kingdom**; three walkers written apart agree lock by lock, and all 68 witnesses replay from word 0 on a separate machine. **None of the six locked scripts the Easymode park places is among them** (the three toilets, `Coconut`, `Bouncy`, `Junspray`: their locks come after at most 29). **But nothing else runs during a script's turn**, so a world answer (`LETMEOFF`, `RIDECLOSED`, `LIMBOSPACE`, `WALKGET`, the clock, a `WAIT4ANIM` deadline) is the same every time one turn asks it. Walked that way, 15 of the 18 cannot happen: a poll loop exits only on its first test in a later turn, and the arrival is the phase plus a fixed path, well short of 49. The three left (`bumper` @92, `GoKarts` @52, `Wateride` @77) survive only because `BUMP`'s answers were taken as free to change within a turn (the bumper family's are decoded since, "How a bumper ride ends a go"; this walk has not been redone with them). **The Hot Pot's `bumper` @92 is the only one the project's interpreter can reach**, because its `BUMP`s are no-ops there: from the turn that resumes at its `WAIT 1000` @134, the ride unloads in one run of 18 instructions a rider, removes cars at 9 each and reaches the lock after `(19 + 18n + 9d) mod 50` costed instructions of its turn, for `n` riders and `d` cars removed. 49 needs 8 riders cut to 4 cars, or 7 cut to 1: the capacity cut during the ride. Its capacity runs 1 to 8 (`MinCapacity`, `MaxCapacity`), red-lined at 4.

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

**A guest heading for the gate runs for the bus while it pulls away.** State 2's turn, `FUN_004ff730`, writes the
hurry `+0xc2` before it walks the guest, from the words at `0x0075c7f0` (0, 25, 50): **50** when `FUN_0051aad0` answers
0 (no vehicle current, or the current one is `mArrivalVehicle_Size1`'s) and `FUN_0051a690` answers 3; else 25 when the
guest's id has its low two bits nought, else 0. `FUN_0051a690` answers -1 with no vehicle current, and otherwise the
vehicle script's variable 1 (`FUN_0055a390( FUN_0055a070( id ), 1 )`, the script found by the thing's `+0x24`), which
is `VAR_STATUS` in all three vehicles' scripts; a 6 it answers -1 for after it writes that variable nought
(`FUN_0055a0b0( script, 1, 0 )`) and clears `mCurrentArrivalVehicle`, so a guest's asking can let go of a spent vehicle
before the manager does. `bus.RSE` sets 3 at instruction 57, once it is let go after a load, and
holds it through its leaving clip (`TRIGANIM 5, 1`) and a second more, so the guest the load dropped is the one who
runs. The log line, `"The bus is coming!  RUUUUUUUUUUN!!!!"` (`0x0075d914`), goes to the bare `RET`. OpenTPW asks the
same (`PeepBehaviour.GateHurry`, `ParkPeople.BusStatus`), and counts the asking's let-go of a spent vehicle
(`GATE_HURRY_FORGETS_SPENT_VEHICLE`; its manager sends it round instead, Q131). Measured in the running game
(`q199run.py`, jungle): the bus answered 3 from `mGameTick` 1309 or 1310 for 39 sweeps, the dropped guest 43
walked at hurry 50 through it and its eased speed rose a quarter of the way to `(50 + base) / 100` a sweep.

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
`saves.md`, "The arrival block", on its `docs/arrival-block` branch). Entering a park zeroes `mGameTick` (`FUN_005156a0`, `0x00515865`, from `FUN_00407e00` at `0x0054ed3f`).
Later in the same pass of state 9, `FUN_005accf0` (`0x0054f12b`) loads the newest `*.TPW*` in the player's folder for
the theme over it (`saves.md`, "Entering a park"), through `FUN_00414d40( path, 0, 2 )` (`0x005ad054`), `FUN_00415270` and `FUN_005179c0`, which reads
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
stop (argument 1, pushed at `0x004cf745` before either arm) and, when `FUN_0051aad0` reports a vehicle standing other than
the small crowd's (`[+0x1da72c]`'s, the bus), subtracts `0x100` from the packed id (`0x004cf75c`). `FUN_004d8650` packs `y * 128 + x + 1` (`SHL EAX,0x7`,
`0x004d8663`), so that is two rows: Lost Kingdom's (53,5) becomes (53,3) for a larger vehicle, and a bus load is made at (53,5). It then allocates `0x22c` bytes and
constructs the person there. **So the vehicles are mechanism rather than transport**: nobody is ever inside one.

**From the stop to the booths: a walk to the roadside, and a wait there.** The constructor `FUN_004faec0` sets a new
guest's first state (`0x004fb1d0` on). On a cell that counts as the park's (`FUN_004fa990`) they take a visitor number
and are made deciding, state 6. Anywhere else, which the bus stop is, they are sent to the crossing's bus-stop side:
`FUN_004d8710( draw & 1 )` is `CrossingBSSideA`'s cell or B's X on A's row, (47,5) or (48,5) in Lost Kingdom, and the
point on it is a second draw's low byte across and a fixed 200 of 256 down (`0x004fb22b`), through `FUN_004fa5f0`;
state 0 with a route, state 6 with none (`0x004fb252`), then particle `0x13`. **Both draws are the guest's own**: the
constructor has just reseeded the generator with the guest's id word (`FUN_00516370`, `0x004fb19e`), and the cell is
the **second** draw from there (the child's bank, `FUN_00541f70`, is the first; the sprite's set-up `FUN_004d4140`
makes none), the place across the **third**: measured on thirteen guests' walker targets, thirteen of thirteen on
both (Q235, `q235/orig/a.log`). OpenTPW's `WalkInDraws` takes the same two (Q235b). State 0 (`0x00501a0f`) walks; arrived, it writes
the facing `0x400` and state 1. State 1 is `FUN_004ff520`: **nothing at all while `FUN_0051a760` answers no**
(`0x004ff52c`, `JZ 0x004ff5ab`), the leavers' own test, so they stand by the road while the bus drives in, unloads
or moves on (unless ten or more are still to drop); then a ticket booth by `draw & 1` (`FUN_004d8610`), a point on
its cell by the low bytes of two more draws, across then down, the walk and state 2. `FUN_0051a760` has those two
callers and no other (`0x004ff52c`, `0x00500b47`). **In the original, predicted first** (the reader `arrivals.py`,
2026-10-06): the load of thirteen was called and the bus summoned on `mGameTick` 1264 with no vehicle current; the
thirteen were made in state 0 on (53,5), one a sweep from 1300; each entered state 1 on (47,5) or (48,5) between 1326
and 1359, thirteen of thirteen within a sixth of a cell of the aim the second and third draws from its id give (guest 53
is aimed at (47,5), 248 of 256 across, and stands just short of it on (48,5)); the load
was let go on 1313 and the
bus read 3 from 1321; **all thirteen went to state 2 on 1360, the tick the bus first read 4, and none before**; they
reached the booths from 1389. **OpenTPW** (the review's fix 1): `ParkPeople.Admit` makes the guest so
(`PeepBehaviour.WalkInFromTheStop`, `WalkInDraws`), `AtGate` waits on `MayCrossTheRoad` and aims at a point on a
booth's cell. Measured in Lost Kingdom, whose load is one guest: made on 1300 walking, aimed at (47.742,5.781), the second and
third draws from id 43 (190 of 256 across; (47.895,5.781) by the fourth and fifth, before Q235b); standing at
(47.756,5.781) on (47,5) from 1328 (the original's guest 43: 1326); let go 1301, the bus at 3 from 1308 and at 4 on 1348, and the
guest heading for a booth on 1348. On the unchanged build it headed for the booth's centre on 1303, the bus still
unloading. A walk stops short of its aim here as it does in the original, so a guest aimed high across a cell can
stand one cell east of it in both (`ride-operation.md`, "Where a walk ends, measured"). **Not the original's**: the
two draws (above), and particle `0x13` is not made.

**Which balance keys these globals are, proven.** Nothing writes them by name: the balance loader stores each
value at a slot its descriptor's place in the table gives it (`park-engine.md`, "How a key finds its global"; the
`PeepInfo` object at `0x00785040`). Walked over all 283 descriptors from the file on disk, the table closes exactly on
the next object (`0x00785828`), and `Arrival.MinPeople` (descriptor 93) lands on `0x00785310`, the floor;
`TimeBetweenArrivals` on `0x00785314`, the period; `FixedRate` on `0x00785318`, which nothing reads (no reference, no
bytes `18 53 78 00`); `NewParkBonus` on `0x0078531c`; and `PointsPerVisitor` on `0x00785320`, the divisor. And
`FixedItemInfo.BusStopAPosX`/`Y` land on `0x007855ac`/`0x007855b0` and `BusStopBPosX`/`Y` on `0x007855b4`/`0x007855b8`,
which is what `FUN_004d8650` reads for arguments 0 and 1; `FUN_004d8690` reads `EntranceA`/`B` the same way
(`0x007855bc` to `0x007855c8`), `FUN_004d8610` `TicketBoothA`/`B` (`0x0078559c` to `0x007855a8`), `FUN_004d86d0`
`CrossingParkSideA`/`B` (`0x007855cc` to `0x007855d8`, one Y for both) and `FUN_004d8710` `CrossingBSSideA`/`B`
(`0x007855dc` to `0x007855e8`, one Y for both). The table's order is the executable's own, not the file's: in the
table `Entrance` and `CrossingParkSide` come before `CrossingBSSide` (names at file offsets `0x340cfc`, `0x340dec`,
`0x340edc`), where `Standard.sam` lists the crossings first. Three were
known by their readers before the walk (the floor, the divisor, and the stop ten arrays further on), and all three land
where they should.

`+0x1da720`, the ushort thing id `FUN_00519510` reads to reach the analyser's counters, is `mParkAnalyser`: the
header's writer `FUN_00516c80` pushes the string `mParkAnalyser` at `0x00516f8c` and pairs it with
`LEA ECX,[EDI+0x1da720]` at `0x00516fa0`.

### What the balance file supplies

The global `Standard.sam`; jungle's own `Standard.sam` overrides none of these, and its `Easy_Standard.sam`, read
over both for an Instant Action park, sets `PointsPerVisitor` to 5:

    Arrival.MinPeople            1
    Arrival.TimeBetweenArrivals  150
    Arrival.FixedRate            15
    Arrival.NewParkBonus         20
    Arrival.PointsPerVisitor     6

Each fills the global named above (proven by the slot table). Read them with `ParkBalance.Int( "Arrival.X",
fallback )`; these five are ordinary single-value keys.

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

Where each guest is made is the original's (Q127): `ParkPeople.ArrivalCell`, stop B, and two rows out for a vehicle
other than the bus. Measured: the original's first load, thirteen guests on `mGameTick` 1300 to 1312, each first seen
on (53,5) with the current vehicle the small crowd's (`+0x1da72a` and `+0x1da72c` both 15); OpenTPW's first load, one
guest, (53,5), a bus load of six by hand all (53,5), and forty by the second vehicle all (53,3), which the original
was not made to send. A current vehicle is reused whatever size the load asked for, there (`0x0051a314`) and here
(`Summon`), and `ArrivalCell` asks the vehicle that is current, so a vehicle summoned for a leaver can be the one a
load then finds. **Not the original's in one way**: when the wanted feature is missing `FUN_0051a2f0` falls back
through the ferry, the seaplane and the bus, where a vehicle with no script here still has its guests made at the
wanted vehicle's cell.

**The spent vehicle** (Q131, decoded first-hand and run in the original; the build is Q131b). A vehicle's script
ends its circuit at status 6 and then spins on `VAR_TRIGGER` (`bus.RSE` 112 to 120). Whoever next asks its status
through `FUN_0051a690` is answered -1, and `FUN_0051a760`, which reads the status the same way and answers only 0 or
1, takes it as no vehicle (and only for the small crowd's vehicle: it answers 1 for any other before it reads a
status, `0x0051a774`). Either asking writes the script's variable 1, `VAR_STATUS`, to nought
(`FUN_0055a0b0( script, 1, 0 )`) and clears `mCurrentArrivalVehicle`; the manager asks on every sweep (its tail,
`0x004cf4b6` on), so a 6 does not outlive the sweep. Nothing drives the vehicle anywhere after that: its last
animation is played (`WAITANIM 5`, 109), its tagged effects are stopped (`KILLOBJ 1`, 115: a kill tag, not a model,
"ADDOBJ", above) and its script spins until a summons. Where that leaves its model was not read or photographed in
the original. `FUN_0051a2f0` is the summons: with a vehicle current it sets that
script's variable 0, `VAR_TRIGGER`, to 1 (`0x0051a663`) and nothing else; with none it picks the slot (the size asked
for, or at random of the three for size 0, `% 3` of the save's seed), and for a vehicle thing that exists sets its
`VAR_TRIGGER` to 1, for one it has just made sets `VAR_STATUS` to 1 (both at `0x0051a5ed`, the variable pushed by the
arm), and makes it current. Who summons:
the manager with a load held and no vehicle answering (`0x004cf489`, by the load's size), and its tail for a leaver
standing at stop A in state `0x15` (`FUN_0051a9d0`; at random). The tail also triggers a vehicle at status 4 when no
leaver stands there, at 0 when one does, and at 2 when one does and the load is all dropped
(`ride-operation.md`, "Q128").

Measured in the original, the bus's script variables read from memory through its first load (`bus.py`; a script is
the node of the list at `[0x008791b0]` whose `+8` is the thing's `+0x24`, its variables the dwords at `[node + 0x1c]`):
on entry no vehicle current and the bus's status 0; on 1094 another vehicle current (thing 43, the bus's script
untouched); the bus current with trigger 1 on `mGameTick` 1279 and status 1 on 1281; 2 on 1315;
triggered on 1328 (its load dropped: `bus.py` reads the script, not the guests); 3 on 1337; 4 on 1376, triggered on the same sweep, 0 on 1378; 5 on
1392; then on 1421 status 0 with trigger 1 and still current: **summoned again at once** (for a leaver waiting at
the stop, by the listing; this log reads no guest), and round a second time (1, 2 triggered on arrival, 3, 4, 0, 5), and on 1550 status 0, trigger 0 and no
vehicle current; on 1591 another vehicle was (thing 32). Status 6 was never caught at a 10 ms poll, as the
same-sweep answer says. **Two predictions were wrong**: that the bus would stay away after its first circuit (a leaver summoned it), and that its thing's cell
would show where it stands (the thing's position bytes read (0,0) throughout: the vehicle is drawn by its script's
object, not placed as a thing).

**OpenTPW** (Q131b): `ParkPeople.VehicleStatus` is `FUN_0051a690`, forgetting a spent vehicle as it answers;
`Summon` is `FUN_0051a2f0` and the one place a vehicle's `VAR_TRIGGER` is set; `StepArrivals` summons the load's
vehicle by size while none answers and sends it on with the same call when the load is all off; `StepVehicle` is the
tail's arm with nobody waiting, a trigger at status 4. Measured in Lost Kingdom beside the sequence above: the bus at
`VAR_STATUS` 0 and pc 120 before the first load; summoned on `mGameTick` 1264 and its guest made on 1300, **36 sweeps
on, the original's 1279 to 1315**; let go on 1301 and spent on 1394, **93 sweeps on, the original's 1328 to 1421**;
then at status 0 again, off the stops; a second load by hand summoned on 1499, its first guest on 1535, 36 again.
Before it, the bus was sent round after its first circuit and stood at the arrivals' stop at status 2, and the second
load's first guest was made on the call's own sweep.

What it does not reproduce, each said at its site: the two refusals, in world state 4 and with the park full, where the original calls a load
of nobody and still sends its vehicle (neither reached in Lost Kingdom; a load that would pass the cap is cut to what fits, counted here on everybody and not on `FUN_004c7fa0`'s crowd); a load saved half-dropped is carried on by its count and flag, and by the vehicle the save names current where that is one of its three sizes' things (`saves.md`, "OpenTPW's writer, the arrival vehicle"; another thing is counted, `SAVED_CURRENT_ARRIVAL_VEHICLE`, and the rest then come by the vehicle their number summons); guests made with no script to ask, on the sweep after the one that calls the load;
and the ferry
and the seaplane, stood as the park loads where the original makes each at its first summons: they stand at their
first spin at status 2 from the start, in view at the arrivals' stop, and a first summons finds the drive in done
and sets no trigger (`ParkPeople.Summon`; making them on demand is Q26c's). (The second load in the older measurement above dropped on the sweep that called
it because the spent bus was then sent round again: Q131b took that out.)

### The headcount score

`FUN_004c8240` is the park's worth, one whole number, and it has two callers: the load's size (`FUN_004cf5b0`,
`0x004cf5fc`) and a guest's judgement of the gate's fee (`FUN_004ff5b0`, `0x004ff5d2`, logged as "Working out how
exciting park is"). It takes nothing but the world. It walks `mFirstObject`'s chain (world `+0x1da746`, next at thing
`+0xc`), and an object adds to the sum only when all three hold:

1. its item's `Info.WhichUIType` (the descriptor's copy at `+0x4ac`) is **0 or 2**, a ride or a sideshow
   (`0x004c82a9`): a shop, a feature and the unlisted kinds add nothing;
2. it passes the offer gate `FUN_004dd920` (`0x004c82be`), the chooser's own (`ride-operation.md`, "Where an object's state
   comes from"): choosable, not broken down or condemned, `mCanLoad` set, a back of queue with room behind it, and for a
   track ride its track;
3. its item's control record counts at least one standing (`+0x18` above nought, signed, `0x004c82ee`; the record is
   `FUN_004d3d80`'s, `hud.md`, "What the buy list actually filters on").

What it adds is **`( AttractionValue + bonus ) / n`**, a signed whole division (`0x004c83fe`..`0x004c8406`):

| | `Bumper.WhichTrackType` (`+0x9c`) nought | any track type |
|---|---|---|
| `n` | the item's standing count, record `+0x18` | 1 |
| the age, in days | since the item's **first** build: `FUN_004f88b0` of record `+0x1c`, `( mGameTick − stamp ) × mFunnySecsPerRealSec / 4` seconds (the subtraction unsigned), over 86,400 | this object's own, `FUN_004dd670` (`0x004c8391`) |

`bonus` is `Attraction[ age / NewAttractionDecayTime ].NewBonus` while that index is 0, 1 or 2 and nought past it
(`0x004c83b5`..`0x004c83e4`, signed). The fields are the item descriptor's `Info.AttractionValue` (`+0x58`),
`Info.NewAttractionDecayTime` (`+0x5c`, at least 1 by its bound) and `Attraction[0..2].NewBonus` (`+0x268`, `+0x26c`,
`+0x270`), named by counting the compiled schema's four-byte slots between `WhichUIType` at `+0x4c` and
`WhichTrackType` at `+0x9c`, both known from their readers. So **two of one flat ride are worth what one is**, each
adding half, and all of them stay new, or stop being new, together, by the day the first was built, a sold one
included: the stamp is written only while it reads nought (`0x004db69c`) and the count alone goes down. Each track
ride adds its whole value and is new from its own purchase. A day is 23.04 sweeps at the shipped rate of 15,000.

**Lost Kingdom's values** (the category files, each item's own and its `Easy_` file; `attraction.py`): every ride
has `AttractionValue` 25 and a decay time of 60 days, with bonuses 10, 7, 4, or 15, 10, 5 for the three track rides
(Dino Karts, Temple Of Gloom, Splish Splash). `SideShow.sam` and the four sideshows set none of the keys, so **a
sideshow adds nought**: value 0, decay 1, no bonus. A ride is therefore worth 35 for its first 60 days (1,383 sweeps,
5 min 43 s), 32 for the next 60, 29 for the next, and 25 from day 180 on.

**The stock park is worth 35, then 32.** `Easymode.TPWI` has one ride, the Belly Bounce, its record's count 1 and
stamp 15 (FileFormats `saves.md`, "The object controls"). At the first load's call, `mGameTick` 1264, it is 54 days
old: 35, and `ftol( ( 20 + 35 ) × 1.2 ) / 5` is **13**, the load Q127 watched. The bonus steps down on tick 1398, so
the second load is `ftol( 52 × 1.2 ) / 5`, **12**; in rain they are 8 and 8. With the Belly Bounce shut, not loading
(`mCanLoad` nought) or its queue full on the calling sweep, the park is worth nought and the load is 4, or 3 in rain.

**In the original, predicted first** (`q26/orig/watch.py`, read-only, 2026-10-08): the load called on `mGameTick`
1264 held **13** people and the next, called on 1916, **12**, neither in rain; record 1100 read count 1 and stamp 15
and the rate 15,000 throughout, and the Belly Bounce stood at state 0 with `mCanLoad` 1 on both calling sweeps.
The function keeps no result, so the 35 and the 32 are the listing's sum over the memory's inputs, stepping on tick
1398 as worked out above; the two headcounts are the game's own. Not met: a load called in rain (it rained from
1398 to 1567, between the two), a ride the gate refuses on the calling sweep, a track ride, two of one item, and a
bonus index past 2.

**The fee's reader** is built already (`PeepBehaviour.Judge`, `ParkAdmission.IdealPrice`; `hud.md`, the
entryprice row): the same sum, taken afresh at each judgement, so a guest judging while the gate refuses the one
ride judges a park worth nought.

**The record's two writers.** The count goes one up in the object constructor (`FUN_004db090`, `0x004db6d0`),
just after the stamp's write-while-nought (`0x004db690`), and one down as the object destructor's first act
(`FUN_004dd0a0`, `0x004dd0e1`), which leaves the stamp. So an item built on tick nought (a fresh park's gates and
lights) keeps a stamp of nought and takes the next build's.

**The load's arithmetic** (`0x004cf623`..`0x004cf648`): `NewParkBonus + score` is loaded as a whole number (`FILD`),
multiplied by the float 1.2 (`0x00700364`) or 0.8 (`0x00700368`), cut to a whole number (`FUN_0067a830`), divided
signed by `PointsPerVisitor` held to 1 or more, and raised to `MinPeople`.

**OpenTPW builds both** (Q26b): `ParkWorth.Of` is the sum, taken afresh by `PeepBehaviour.ParkExcitement` for a
guest's judgement and for `ParkPeople.StepArrivals` as it calls a load; `ParkWorth.LoadSize` is the arithmetic.
`ParkWorld.ObjectControl` reads the count and the stamp, `ParkState.BuiltOf` keeps them as things are bought and
sold, and the item's three bonuses are `ItemDescriptionFile.NewBonusAt`. The gate is `ParkRideChoice.CanBeOffered`
and a track ride's age `ParkState.AgeInDays`. With a load above one, the vehicle is chosen by its size (36 and 61),
which no stock park reaches: three rides in their first 60 days make 30 a load.

**In the running game, predicted first** (`q26b/confirm.py`, the stock park left alone, 2026-10-08): the first load
called on `mGameTick` **1264** with **13**, the park worth 35, dropped one a sweep on 1300 to 1312 and let go on
**1313**; the second called on **1916** with **12**, the park worth 32: the original's two sizes and its three
ticks (`q26/orig/a.log`: 1264, 1313, 1916), read again in the original the same hour, predicted first, 3 of 3
(`q26b/orig/a.log`), and photographed there at the stop on tick 1327 beside OpenTPW's
(`q26b/sheet-ours-beside-original.png`). The unchanged build calls 1 on 1264 and 1 on 1904.
**The second load is not steady here**: a second run of the same build (`q26b/gate`) called 13 on 1264 and then
**4** on 1916, the park worth nought, because sixteen guests filled the Belly Bounce's sixteen places on that
sweep and the offer gate refused it: the listing's own arm, reached by a crowd on the Belly Bounce the original
does not have (Q222; its ride passed the gate on every sweep of both of today's runs). The five guests the
save leaves at the booths judge `GP` 25, 27 or 28 against the fee of 25 where the unchanged build's read 20, and
from tick 1398 the arrivals judge 25, 26 or 28. One fact the item's note asked to be re-checked: with the park worth
35 a fee of 30 is under easy mode's expensive line (37.5 at the least) and is paid; 30 sulked only against a park
worth nought (Q145).

**What a new guest's fields come from** is decoded in
[guest-arrivals.md](guest-arrivals.md): the constructor's initial meters, cash and exit variation,
prankery, draw order and ID reseed. The exit counter loses one per needs tick, with no clamp;
the state-6 turn sends a guest home when it reads exactly zero
(`ride-operation.md`, "The state-6 turn, in order", arm (d)).

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

**The rule that GENERATED `mNeighbours` is `FUN_005348d0`, and the reason no sweep could ever fit it is that it is INCREMENTAL and ORDER-DEPENDENT.** Full decode in `docs/exe/park-engine.md`, "Building and deleting paths and queues". A sweep of member sets x diagonal rules tops out at **67/78**, and splitting the member set so cardinals admit `{1,9,10}` while diagonals admit only `{1}` reaches **73/78** — but no pure function of the final map can reach 78, because:

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

So **anything that computes its own normals in engine space must hand them over with Y and Z already exchanged**, or that line turns them on their side: a flat ground's `(0,0,1)` becomes `(0,1,0)` and level land is lit as though it were a wall. Ground lit that way renders dark and muddy, and **it is invisible in a build, because nothing is wrong except the picture.**

Related: in the lobby, `vAmbient = g_flAmbient > 0.0 ? g_flAmbient : 0.4`, so geometry is never unlit. In a park, `g_flParkLight` switches the shader to the original's lighting, per vertex: the ambient colour `g_vAmbientColour` plus the sun's colour times the facing to `g_vLightTravels`, clamped (`park-engine.md`, "The lighting model"). **If something looks black, suspect the normal, not the light.**

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

**Bare-only-M models outside `levels/`: zero**, so the bare-file probe cannot touch the lobby. But **91 models ship more than one numbered M clip**, and **8 of them are lobby models** — `*_gate` with 3 and `*_isle` with 2, in all four themes — plus the advisor with 15. `MeshAnimator` cycles every clip in turn and `MeshRotator` the first two (`ClipsUsed = 2`), but neither loops a gate: `LobbyGate` poses M1 once as the park-entry flight swings onto it and M2 when Escape cancels the flight, each over the span it declares (`lobby.md`, "Escape cancels the fly-in, and the gate is the flight's"). **So a channel must be something a park model opts into, leaving lobby playback exactly as it is.**

Known divergences still open, both in lobby playback, which a park thing does not use: `MeshAnimator` and `MeshRotator` keep private clocks, and play `FirstFrame..LastFrame` (`MeshRotator` only as far as `MotionEnd`) where the engine plays **0 -> declared span** (164 clips disagree). For a park thing the per-clip hide list is still unread, and `PoseAsBuilt` hand-rolls its effect after the build.

`AnimationFile.VisibilityTrack` already matches the engine's visibility rule exactly. `AnimationFile.RotationTrack.Ease` obeys the easing table. The per-clip hide list is **still unread by us**.

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
| `md2dump/` | the whole corpus already extracted: **1,796 `.md2` files** (1,780 from the 306 level archives, plus the advisor's 16), one directory per archive keyed by its path under `data/`, `/` written `__` (`levels__jungle__features__gates`, `levels__jungle__terrain`, `global__advisor`), so nothing collides. **Sweep this tree rather than rebuilding it** |
| `rolecensus.py` | applies the engine's own probe rule (numbered from 1, bare only if that run found nothing) to every archive's own name table — no decompression needed. Pass `--by-archive` to reproduce the older, narrower per-archive number |
| `rsewalk.py` | carries the `.RSE` format, the 106-entry table and every check above. **Re-run it rather than writing another.** Its header's extraction recipe keeps a per-wad subdirectory — keying output by the wad's own folder loses the theme (every script sits in a `rides`/`shops`/`features` folder) and silently collapses 262 wads into 203, overwriting scripts |

**`wadcat` is not on PATH**: its location is in `CLAUDE.local.md`, and it is **the DEBUG build, which matters.** A stale `bin/Release` copy is still on disk and its own usage line offers only `--list|--id|--bounds|--meshes|--anim|--cat`: **no `--dump`, no `--field`, no `--heights`**. Reaching for the Release path and finding `--dump` missing **looks exactly like a broken tool and is not one** — it is an out-of-date build. Its interface is `wadcat --list|--id|--bounds|--meshes|--anim|--field|--heights <wad>…` or `wadcat --cat|--dump <SUFFIX> <wad>…` - **the suffix comes first**, there is no `--out`, and **`--dump` is the one that EXTRACTS files**, into the working directory. Invoked wrongly it does nothing quietly, so "no scripts were dumped" reads exactly like "these archives carry no scripts". `--cat` writes nothing to disk: it prints every matching member to stdout behind banners, and has produced a false finding. `wadcat --anim <wad>` prints each clip's readability and its rot/morph/uv/pos/vis counts, which is how "what does this clip actually drive" is answered. Note `wadcat` is built against **our own `AnimationFile`**, so its `frames a..b` is the KEY-derived span and its `readable` is our `IsValid`; **it cannot independently check a declared span.**

A third read method — `memory.getBytes` into a Jython bytearray — **silently returned zeros and was discarded as a dead instrument** rather than believed. Two agents hit that same bug; no conclusion here rests on it.

**Three clip totals are all correct and answer different questions. Do not read a mismatch as an error:** **1085** clips summed PER SCRIPT (308 `.RSE` files across 262 archives; an archive with several scripts counts its clips once per script); **910** per archive-named item (274 of them); **1273** per base model across all of `data/` (445 of them). Likewise the item census: keyed on **base models** — any `<stem>.md2` with at least one role file beside it — the answer is **197 of 445**; the older "88 of 274" keyed each item to its archive's basename, which is invisible to every archive holding more than one base model (`terrain.wad` holds `base.MD2`, `queue.wad` holds every queue piece, and the go-kart and water-ride archives hold a track segment each).

## OpenTPW catch-up ordering reproduction (2026-10-03)

Observed in OpenTPW, not an additional executable claim: `ParkCatchUpOrderTests` drives
`ParkRides.Update` then `ParkPeople.Update`, the order used by `Level`. A synthetic script
with scheduler id 1 yields at tick 1 and clears `VAR_ADMIT` at tick 9. A guest from the shipped
Lost Kingdom park is placed in the entering-ride state and queue for thing 13.

Across 16 ticks, partitions of 1 or 8 ticks per frame complete boarding on relative world
tick 2; one 16-tick frame completes it on relative world tick 1. The first guest turn sees
future script state because all ride ticks have already run. The test asserts this known
Q150 discrepancy, not correct behaviour. Replace its unequal expectations when the scheduler
is repaired. This isolates admission timing; it does not establish that every subsystem
should be partition-invariant (frame animation and original script time have separate rules).

## Repeatable OpenTPW simulation tests (2026-10-03)

`ParkPeople` accepts separate optional arrival, guest-decision, ride-settlement and staff
random generators. Omitted arguments retain separate default generators. This is a test
control for the current simulation, not a reconstruction of the original shared RNG.
`ParkReplayTests` runs 400 eight-tick updates per replay, comparing guest/staff state,
positions, destinations, cash, happiness and appearance after every update. Every supplied
random stream must actually draw; a costume-shop dismissal exercises ride settlement.
Identical seeds and frame inputs reproduce the trace; different seeds change it. Removing
random injection fails the test. `SimulationClockScope` restores frame/tick clock internals.
This does not prove determinism of weather, rendering, audio or a complete `Level` session,
and does not require different frame partitions to agree while Q150 is open.
