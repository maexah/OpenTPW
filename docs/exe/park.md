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

`base.map` / `terrain.map` are self-describing. Exactly five files in the game open `TP2M` (the four parks' `base.map` and jungle's `terrain.map`), all 16,464 bytes, and the layout holds in all five:

    0x00  "TP2M"
    0x04  "Theme Park 2 Attribute Map File\0"   (32 bytes)
    0x24  "MAP "                                 chunk tag
    0x28  uint32 chunk length = 16412
    0x2C  uint32 width  = 128
    0x30  uint32 height = 128
    0x34  uint32 x5     = 8, 8, 8, 8, 8         (20 bytes, in all five files; meaning unknown)
    0x48  128 x 128 attribute bytes              <- body starts at 72, NOT 80
    0x4048  "END " + uint32 0                     (8 bytes: 44 + 16412 = 16456, plus this trailer = 16464)

The MAP handler (`0x004d8cf0`) reads width at payload+0 and height at +4, then steps over the five words
(`add esi,0x14`, `0x004d8d17`) and copies the cells from payload+0x1c, file `0x48`: it never reads them, so they
have no name in the original (upstream look 2026-10-19, `verify-terrain.md` #1, #2). It places the byte for file
position (i, j) at cell (i - `MapInfo.XStart`, j - `MapInfo.YStart`), both 0 in every `Standard.sam`.

**Indexing is `index = x * 128 + y`** — X is the major axis. Proven, not assumed: all ten `FixedItemInfo` positions from `Standard.sam` land on a meaningful attribute under `x*128+y` and on 0 under `y*128+x`. TicketBooth (47,13)/(48,13) -> 148; BusStop (42,5)/(53,5), both Crossings and StrikeAreaStart (40,9) -> 144; Entrance (47,17)/(48,17) and FixedItemOrigin -> 8.

Attribute values seen: 0 (empty), 1, 3, 8 (park entrance), 17 (0x11, the bus/road approach, 490 cells), 128, 144 (0x90, road/bus/crossing, 48 cells), 148 (0x94, ticket booth, 14 cells). They look like a bitfield. **Values 17, 144, 148 and 8 occupy byte-identical positions in all four parks** — the fixed infrastructure every park inherits. The rest differ per park: the 0/1/3 cells, jungle's four 128s (x 51..54, y 57) and one cell of 16 in fantasy (27,17).

The attribute map's non-zero region is exactly x 0..95, y 0..84: the heightfield's 96x85 cells (`base.md2`'s), not `Standard.sam`'s `MapInfo` 95 and 84, which are the limits the picker holds a cell below (0..94, 0..83; `park-engine.md`, "The picker never hands the mode a cell off the map"). Value 17 (the bus road) occupies x 28..67, y 0..16; value 3 occupies x 29..57, y 39..64.

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

`basem.MD2` reports `IsAnimation: True` with 0 meshes, exactly as `ModelFile` documents. It is `readable True` with **11 UV tracks over frames 0..100** — UV scroll is how this game moves its river, as `Jun_isleM1` laps the lobby island's shoreline (the park's sea by the gates is the engine's own moving quilt, `park-engine.md`, "The park's sea"); OpenTPW's park log binds them to eleven meshes, the nine `surface*`, `Object12` and `falls02`, the waterfall (`4f307bb`). **Only jungle ships a `basem.MD2`**; fantasy, hallow and space ship `base.MD2` alone.

**A first park render needs no new file format**: `base.MD2` through the existing `ModelFile`, its textures through the existing WCT/WAD path, and a camera. `.MAP`/`.TCT` are needed for gameplay (attributes, editing, path tiles), not for the first picture; `base.lnd` is not needed at all.

## `.LND` — procedural-texture source, not the landscape

Its bytes are FileFormats `lnd.md` (Q483): a version byte 3 (`FUN_00560ff0` refuses any other), five ramp pairs, a
map of 384 x 344 samples of four bytes (four samples a cell; byte 0 says where the ground is composited, bytes 1 to 3
are weights), then three sets of thirteen textures, for 64-, 32- and 16-pixel tiles. Sizes are park-specific (jungle
2,417,116; fantasy 2,196,802; hallow 2,447,361; space 2,241,690). The heights are not here: they are a block inside
`base.MD2`, read by `HeightfieldFile` — see `park-engine.md`, "The heightfield lives inside base.MD2". The engine loads
`base.lnd` only when `DAT_007a1a8c & 0x2000` is set; what it makes of it is `park-engine.md`, "The procedural ground".

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

**`MapInfo` 95 and 84 are the limits the picker holds a cell below** (0..94 and 0..83; `park-engine.md`, "The picker never hands the mode a cell off the map"), inside the **96x85 heightfield** of `base.md2`, which sits inside a 128x128 map. `Easy_Standard.sam` has **no** `MapInfo` — it is an override layer over `Standard.sam`, not a replacement. `global.sam` is the small file the **lobby** reads before a theme loads (ticket rules, `ParkName.GateObjectId 1601`).

**The base `data/levels/Standard.sam` is the simulation's balance file**, 588 lines against jungle's 144, and it is the layer the theme file overlays. Its groups: `PeepInfo` (**33 keys**), `PeepTypes[0..7]` (8 rows of `PreferredExcitement` / `StartingCash` / `BoredomThreshold`), `StaffPoolInfo`, `Arrival`, `AllStaffConstants`, `PerGradeStaffConsts[0..4]`, `PerTypeStaffConsts[0..4]`, the four `*ConstsPerGrade`, `BankAccountInfo`, `LoanInfo[0..7]`, `Research`/`ResearchTech`/`ResearchCategories`, `Costs`, `Challenges`, `GoldenTicketGlobal`, plus the `MapInfo`/`FixedItemInfo`/`ThemeEngine`/`Seasons`/`Weather` groups a theme overrides.

**The peep simulation's constants live here, not in the exe.** The block at `0x00785000` that the exe's peep code reads is **all zeros statically** — it is filled by the `BalanceLoader.cpp` parser from this file at load. So the tunables were never something to reverse engineer, and `ParkBalance` (which layers base + theme) can read them: `Balance.Int( "PeepInfo.ExitLevel" )` works. The keys are self-documenting — the file carries trailing comments like *"toilet level above which peep is 'desperate'"*. **Themes barely touch it:** the theme `Standard.sam` files override only `PeepInfo.ExcitementToCostDivisor` (fantasy and space, 4 -> 5), and jungle's `Easy_Standard.sam`, read over both for Instant Action, changes two peep keys: `AveragePriceMultiplier` 1.25 -> 1.5 and `ExpensivePriceMultiplier` 2.0 -> 2.5.

### The parks really are re-skins

All four `Standard.sam` files declare **identical** `MapInfo` (`HeightfieldWidth` 95 and `HeightfieldHeight` 84, the picker's limits, at 0,0; FixedItemOrigin 48,17), identical `LightNormal` (0.4, -0.8, 0.4), identical `WaterStartPos` (380, -5, -180), identical `AmbientLightLevel` and `DirectionalLightLevel`. **Only `ThemeEngine.FogColour` differs.** One park engine serves all four — build it once, properly.

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

`test.tpt` (jungle + space only, 236 bytes) is a weather table: dword chunk count 6, then per chunk a 4-byte tag, 4-byte size, payload — `WTHR`, `TEMP`, `WNDS` (wind speed), `WNDD` (wind direction), `CLCV` (cloud cover), `ADSR`; each carries a count then five (value, weight) pairs. Decodable by inspection; named "test", and the executable holds neither `.tpt` in either case nor the tags `WTHR` or `CLCV` in either byte order, so nothing there opens it by name or reads its chunks.

`scape.omp` (409-590 bytes, one per park) is an `OBJ_` record file ending in an `INCL` chunk naming the C++ headers it was built from: the land's own sounds and, in the jungle, its waterfall's spray ("The land's own particles", below; FileFormats `omp.md`).

## Buildable items: the per-item archive

An item is a `.wad` under `features/`, `shops/`, `rides/` or `sideshow/`, standing in for a directory of its own name, and everything inside is named after that stem: `<stem>.sam` (its description), `<stem>.MD2` (its model), `<stem>.hmp` (its height over each cell of its footprint, with the footprint's marks: FileFormats `hmp.md`), `<stem>.sgn` (a name board — rides, and the `gates` and `sign1` features), plus `textures/` and `stexture/`. Jungle holds **67** items across those four folders. **A coaster WAD also carries `GTexture/`**, the track's textures (FileFormats `sam.md`): the coaster loader `FUN_0042fad0` formats `"%s\GTexture"` (`0x0074cb8c`) and hands it to the track builder `FUN_0042fd90`.

**An item ships only the art unique to it.** Everything else comes from the theme's shared archives — `sharetex.wad` (full size, pairs with `textures/`) and `ssharete.wad` (low detail, pairs with `stexture/`), 116 members each, and **all four themes ship both**. Without that fallback the eleven objects Lost Kingdom places were missing **40 distinct textures** and drew the not-found art on most of their surfaces. Every missing name was present in both archives.

**An item's footprint is the BOX its `Info.Shape` picture is drawn in, not the cells marked inside it.** `4x4rock` draws 14 stars in a 4x4 box, `5x5rck` 23 in 5x5, and `ground`/`groundc`/`mystery` draw none at all — yet every one of the 70 jungle item archives (those 67 and the three in `upgrades/`) has a `.hmp` whose length says `48 + 27 * (width * height)`. **`gates` is the only jungle item carrying `Engine*Override` keys**, and it needs them: its picture is a single cell where its real footprint is the 6x3 its own `.hmp` declares.

The model is authored with its **footprint's corner at its own origin** — a 1x1 toilet's floor spans 0..10, the 3x3 fountain's 0..30, the 2x2 staff room's 0..20 — read from node `WorldTransform`, never from bounds, which are node-local and say only how big a mesh is.

**Every item's model opens with a flat floor plate as wide as its whole footprint** — `J_WC`, `wf_floor`, `js_base`, `cn_floor01`, `jb_floor`, `jc_base` — and all 44 footprint cells carry a real ground index, so the ground must skip them or the two fight for the same depth. The original ORs `0x101` into those cells' flag words (`FUN_0046df50`) and keeps their texture word, and its terrain pass skips every cell with `0x1` (`FUN_0056f670`), so it draws no ground under a footprint; its frame still shows green where a plate's border is clear: the floor's texture is replaced by a composited ground tile (`ProceduralBase_BlendFloor`, `0x0057ace0`, Q617, built by Q649, `park-engine.md`, "A built thing's floor"), and the model's loader sets the floor's materials `0x800` (`ProceduralBase_SkirtFlags`, Q360, Q361, `textures.md`, "What a floor's clear border shows"), whether that still matters not read.

## `.sgn` — one layout, walked in order

`FUN_005eb8a0` inks a line: where the glyph mask (`+0x38`) is zero it writes four zero bytes, and elsewhere it
reads the line's fill tiled by modulo, row less `+0x28` over `+0x24` and column over `+0x3c` (decompilation;
`SignFile` letters in the flat ink colour instead, an approximation).
`SignFile` reads all 84 signs by walking the header in the engine's own order (`FUN_005ec3a0`): 61 leave the
artwork byte at `0x08` clear, and three (jelly, zob, C_SCAT) omit both ink blocks, which is what made the header
look like it had two fixed sizes. The layout is in the FileFormats clone, `formats/sgn.md`. The `.sgn` is always named after the
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

The heightfield's 96x85 cells -> world 0..960 x 0..850 (`MapInfo` 95 and 84 are the picker's limits, not its size). Map 128x128 -> 0..1280. `base.MD2` spans -555..1511 x -621..1445, so the park sits inside a much larger scenic island.

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
| `FUN_004647a0` (`RideSystem_LoadModels` in the project) | - | save load: overwrites every animation channel with the saved state and restores the per-node flag words with it | read |

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

`FUN_004735d0` advances one channel, called once per channel from `FUN_00473c70` (stride 0x38, correct), called by two routes. **A model on screen** is advanced and posed from its scene-node callback `0x00463030` (installed by `FUN_00463060` at `0x004638bd`) which calls `FUN_0044e380( model, 1 )` and then marks it `+4 |= 0x200000` (`0x0046304d`); **every other model** is advanced, not posed, by the per-frame model sweep `FUN_0044e410( 3 )` with `( 0, 8 )` (a model whose header `+0x30` carries `0x4` still has its node-lookup records' matrices composed by `FUN_0044ab30`, `0x00473e01`..`0x00473e1b`: 25 shipped models, all coaster parts; `ride-operation.md`, "How long a leg lasts, and where its ends are"), which skips and clears that mark (`0x0044e4a4`, `0x0044e4e0`) — the routine `0x0054e2b0` that the park's load hangs on the scene (`0x0054ecbc`) and the scene draw at `0x0054fb6c` runs through `Render_DrawView` — **after the 31 ms catch-up loop's back edge at `0x0054f8da`**. The park's `FUN_0044e410( 2 )` at `0x0054fa96` advances only the model at `DAT_00790988`. It takes **no time argument**: it reads `DAT_007b496c` (or `DAT_007b4974` when channel flag `0x40` is set), a snapshot written once per frame by `FUN_00473440` at `0x0054f475` from the clock object at `0x785970`.

**Animation advances once per frame off one snapshot while scripts tick at 31 ms** — several sim ticks in one frame still produce exactly one advance. Confirmed by call graph: `FUN_004735d0` has **exactly one** caller, `FUN_00473c70` at `0x00473d2e`, and **not one** of that function's ten call sites is the script system `FUN_005516b0` or is called directly inside the 31 ms loop; the park's are `FUN_0044e410(2)`, `FUN_00429df0(0)` and `FUN_00429df0(1)` after the loop's back edge (`0x0054fa96`..`0x0054faae`, past `0x0054f8da`), and `FUN_0044e410(3)` and `FUN_0044e380` (`0x0044e3cc`, from the scene-node callback) inside the scene draw, so both routes advance once a frame, after its ticks. Of the rest, `FUN_0044e510` advances the terrain's base model (`DAT_0079fcb4`, which `FUN_004504c0` fills by the name `Base` from the level's `Terrain` `base.md2`; called at `0x0054e322` by the park's scene routine `0x0054e2b0` after its sweep, so inside the scene draw too, and at `0x0054e72a` in the state machine's case `0x0054e6c7` after that case's own `FUN_0044e410(3)`), and five advance a coaster track's pylon models (`FUN_00430590` twice, `FUN_00430ed0`, `FUN_00432df0` twice; `FUN_00430ed0` is the track knot pass, `0x7a4610[node+0x70]` at `0x004311f0`: fork watch 2026-10-16, `verify-world.md` L2; the other four were not re-read); those the loop can reach, when a loaded save's layout is replayed (`FUN_00516380` at `0x0051669f`, down to `0x004311f0`) and when a model is built with flag `0x200` (`0x00463860`, through `FUN_004368f0`, `FUN_00436ac0`, `FUN_00434060` and `FUN_00433bd0` to `FUN_00432df0`). None is a placed thing's own model.

**Two candidates were checked and cleared, and either would have inverted this:** `FUN_00473440` runs once per frame *above* the loop, so it looks like the sweep, but only takes the frame's clock snapshot `DAT_007b496c` (the one the advance reads) and the frame delta × 0.001 into `DAT_007b497c`, and advances nothing; and `FUN_00475360` does run inside the loop every 2nd tick, but is a periodic-task scheduler (`+0x7c` due time, `+0x80` interval) that calls none of this.

The consequence for OpenTPW: the advance belongs in the frame sweep, **not in the tick**. An advance in the tick leaves end-of-frame state the same, but a clip ending mid-catch-up promotes its queued successor early, where the next tick's instructions can see it.

**Two gates, and they invert the idle default.** `FUN_00473c70` advances no channel and fires no default unless `model+0xa8` is non-zero (`0x00473ccf` jumps to the node-lookup tail at `0x00473df7`, which still calls `FUN_0044ab30` under its own tests), and the sweep will not call it unless `model+0x14` is non-zero. `FUN_00473e30` parks every channel at role 12 on load, and `+0x14` is raised by a start (`0x004731b4`..`0x0047320a`) or by the RSYS restore's recount of the channels below role 12 (`FUN_00472ee0`, called at `0x00464cde`), so a loaded park's restored loops play with no trigger. Where the default can run, it waits for a clip to finish; then it fires every frame channel 0 reads finished, three attempts at most, with flags 8 from the off-screen sweep (`0x0044e4b7`: no rest restore, no hide list) and 0 from the on-screen route (`0x0044e3c9`) and `FUN_0044e410( 2 )` (`0x0044e42f`), which lay the rest pose and apply the hide list. **It never fires on a placed thing's model**: `FUN_004dd500` builds one through `FUN_00463060` with `0x361` or `0x32f` (`0x004dd5b7`, `0x004dd64f`), whose low bit sets `+4 |= 4` (`0x00463190`..`0x0046319a`), and `FUN_00473c70` skips the default on `+4 & 0x8004` (`0x00473c83`); a loaded park rebuilds each with its saved creation flags (`0x0046424e` → `0x00464942`). So a thing's channel 0 changes only when a script triggers it. Where the default does run, `FUN_00473490` counts a **held** channel 0 finished once it has been held longer than its clip (`0x004734ea`..`0x00473524`).

**A newly built thing starts channel 0 on role 0, frozen** (`ride-operation.md`, the `FUN_00463060` row): `FUN_004732a0( model, 0, 0, 0, 1.0, 0 )` at `0x00463205`, flags 0 so the clip is bound, then `FUN_004732a0( model, 0xd, ... )` at `0x0046321f`, after `FUN_00472d10` found role 0. So a bought thing is drawn on its construction clip's frame nought from the frame it is built until its script's first `TRIGANIM 0` plays it, and so is OpenTPW's (Q623, `RideAnimations.StandOnBuildClip` from `ParkRides.BindNew`; a model with no role 0, such as a vehicle's, stays idle at role 12). Before Q623 ours stood idle until that tick, drawn as `ParkObjects.PoseAsBuilt` leaves a model (the clip's end: finished), and the Round Fountain showed whole for about two ticks, then dropped to its bare floor and rose: Alexah's "flashes". A placing click is dealt in `Hud.Update`, after the frame's object sweep and before its draw, so the model is also posed at once (`ParkObjects.PoseNow`); without it the click's own frame drew the finished fountain (`q623/run4m`). Measured: the original's film from the click shows the footprint, then the bare floor with sparkles, then the build (`q623/orig/a`); ours the bare floor from the click's own frame (`q623/run4`). None of the five scripts that use `GETANIM_CH` asks before its first trigger: all open with `WAITANIM 0, 0` at word 2 and three `TRIGANIM_CH` before their first branch, and their first `GETANIM_CH` is at word 176 or later.

**The idle default is role 5 entry 0 on channel 0**, restarted when channel 0 is idle or finished, gated on `(model+4 & 0x8004) == 0`. Entry 0 is a literal — **nothing in the engine ever advances to a role's next entry**, so cycling every `M` clip has no engine counterpart. `model+0x14` gates whether a model is advanced at all: it counts channels with role < 12, and a stopped model drops to nought and is never restarted — so an unconditional idle restart would start role 5 on every static prop in the park.

### What a drawn frame shows between ticks (Q642)

**The engine draws every mover at the frame's own time, not at the last tick.** Each kind its own way:

| Mover | How a drawn frame places it | Where |
|---|---|---|
| A clip: a ride's, shop's or sideshow's model, the bus, the ferry and the seaplane (their route keys), the river's UV scroll (the terrain's `basem` clip, `FUN_0044e510`) (the bus, ferry, seaplane and river read in the listing, not filmed) | `AnimFrame` (`+0x20`) is a **float**, `(snapshot - StartAnimTime) × speed × 0.03` (`FSTP` at `0x00473740`), the snapshot `DAT_007b496c` the clock read once a drawn frame above the tick loop (`Anim_TakeFrameClock` `0x00473440` at `0x0054f475`, which also leaves the frame's delta × 0.001 in `DAT_007b497c`); the pose blends rotation keys by the frame's place between its two keys, eased (`Anim_FindKey` `0x00470b60` searches by the truncated frame and returns `t = (AnimFrame - key_i) / (key_next - key_i)` from the float at `+0x20`, remapped through the key's nine-step ease bytes where the key's word `+2` is not `0xffff`; `Anim_BlendRotationKeys` `0x00474490` lerps or slerps keys i and i+1 by it) and a morph by `frame - trunc frame` (`Anim_PoseTrack` `0x00471860`). So a clip moves on every drawn frame | `AnimChannel_Advance` `0x004735d0` |
| A track ride's cars | stepped every tick, then drawn eased by `(now - the last tick's end) / 31` (`0x00700f8c`), clamped to -1..1 and so -1..0 past the loop: back along the step's velocity and turn, between the last two steps | `TrackRides_DrawAll` `0x00519060` at `0x0054fa33` → `TrackRide_DrawCarsEased` `0x00546280` |
| The flying cars | the same over their every-second tick, `(now - [0x0087879c]) / 62` (`0x00700f90`): the routine walks the tour ride records, twenty cars a record (`0x0055ce80`..`0x0055ce90`), and caps the argument at 2.0 (`0x007010b8`) with no lower clamp. That stamp is written only on even ticks (`0x0054f5ea`), so after the loop the argument can be positive, roughly (-0.5, 0.5]; the rest of the body was not read | `FUN_0055ce60` at `0x0054fa57` |
| The peeps | the same over the thing sweep: `(now - [0x00878a1c]) / 248` (`0x00700f94`) | `Peeps_PlaceForDraw` `0x00518f90` at `0x0054fa85` (`ride-operation.md`, "Where a WALKING peep is drawn") |
| The sea and the gates' flags | their phase gains 0.4 a drawn frame (`park-engine.md`, "The park's sea") | `FUN_0053c3f0` |

`now` is `0x008786bc`, read just after the snapshot; each stamp is the end of its step (`0x0054f4c7`, `0x0054f5ea`,
`0x0054f683`), since the loop adds 31 before it ticks. Nothing is extrapolated. A sprite's picture still steps, at
62 ms (`FUN_00475360`).

**OpenTPW** eases the guests (`ParkPeople`) and the track cars (`ParkBumperBoats`, `GameClock.PartialTick`) as the
engine does, and moves the river (`ParkTerrain`), the sea and the flags on frame time. **Since Q675 its clips are
posed at the frame's own moment too**: `ParkRides.NowMilliseconds`, the last tick's instant and the part of a tick owed,
truncated to the millisecond and never 31, read by the sweep (`ParkRides.OnUpdate`), each Hot Pot boat's own clip
(`ParkBumperBoats.Sync`) and a save's moment (`ParkRides.Written`: a clip held or replayed in a frame's sweep is
stamped past the last tick's instant, and a file's moment before it would load that clip started in the future, read
through the unsigned subtraction as run past its end). The phase still differs: difference 7 below.
**Measured** (`q642/`, `PREDICTION.txt`), the stock park, the original's camera 530,250 zoom 70 rotation 180 in both,
`rectfilm.py` on the Bouncy Dino's head (its loop, role 2): the original capped at 60 frames a second (`original.sh`
`ORIG_FPS=60`, Wine's `+fps` 59.6 to 60.3, its clock true) changed 58.6 times a second, a picture a drawn frame
(`orig/O2`; 58.74 again in `q675/O`); ours at 143.9 frames a second 32.1 times, a picture a tick, before Q675 (`runB2`;
`q675/C`, the build with the bug put back), and 133.1 times after, a picture a drawn frame (`q675/F`), and not at all
paused. The bus's roof driving in changed 150 times in 6 s with 120 gaps under 14 ms (`q675/F`; 0 of 49 in `q675/C`).
Under `play.sh`'s 30 fps cap the original cannot show more than 30.

### End of clip is five outcomes, not three

A channel on role 0 found past its end is first marked `0x8` (`0x00473758`), and every advance of a channel on another role takes the mark off (`0x004738b2`; `saves.md`, "OpenTPW's writer, a channel's `0x8`"). The queue is promoted first, whatever the model's flag word holds (`0x0047375d`). Failing that, `model[+4] & 0x18` chooses: clear, replay when flag `0x1` else pseudo-role `0xe`; else if `0x8` clear, **stall** (pin elapsed to total, set `model[+4] |= 0x40400000`, defer a stop to the end of the same advance, after the pose is applied, which parks the channel at role 12 with its pose kept, `0x00473a30`); else **stop and call out** to the dispose/reposition group. `FUN_00473e30` sets `0x10` on a thing's model whose only clips are role 0's (`0x00473f32`): 22 Lost Kingdom items, each running one `WAITANIM 0, 0` and nothing that reads the channel afterwards. The replay's speed comes from `+0x30` when non-zero, only falling back to `+0xc`.

**"Freeze" is not "stop posing".** `FUN_00472f60`'s `0xe` pins `elapsed := total`, so the clip is posed **once more at its true final frame** and held; `FUN_00471860`'s early return never fires for it. A rest-restore does exist (`FUN_00472310`, master -> instance, differential at a clip switch) and the freeze paths suppress it by passing bit `4`. **In blend mode (`model+0x30 & 4`) the rest pose is re-laid every frame and a finished channel snaps back** — the opposite of freezing.

**Continuity is a move of the start stamp, not an accumulator.** `FUN_00472bc0`: total is `(float)(uint)(clip[+8] - clip[+4])`; a reset stamps `+0x10`, `+0x14` and `+0x18` from one snapshot; an overrun instead sets `+0x10` and `+0x18` to `+0x14 - trunc(carry * 33.333.. / speed)` with the carry clamped to the new clip's own total. **A float delta accumulator cannot express this.**

### Channels are per-model, unbounded, and one function is buggy

Channels are per-model animation players, count at `model+0x0e`, array at `+0x10`, stride `0x38`, and **none of the three functions that index them bounds-checks against that count**. A model is built with as many as its creator asked for — one for scenery (eight call sites), five for a coaster's trains, a per-thing record field on the main path. **The channel count is not model data.** The corpus names channels 0 to 3; `jungle/rides/totem` uses 3 and `jungle/sideshow/junspray` drives channels 0, 1 and 2.

**`FUN_00473490` does not survey the channels.** `ECX` and the role are loaded **outside** the loop and the pointer is never advanced, so it tests **channel 0** count-many times, and it **writes** as well as tests — refreshing channel 0's `+0x14`/`+0x18`/`+0x20` count-many times and answering "all finished" from channel 0 alone. **It is a shipped bug**, and it matters only where the idle default runs, which is never a placed thing's model (above): there a multi-channel model whose channel 0 has finished is called finished whatever its other channels are doing, and gets role 5 restarted on channel 0 beneath them. The 3-attempt cap in `FUN_00473c70` is load-bearing too: with no role 5 the predicate stays true for ever.

## Clip content: tracks, hide lists, rotation, visibility

| Address | What it is | Notes |
|---|---|---|
| `FUN_00470b60` | the key search (`Anim_FindKey`) | `param_4 == 1 -> 4` (position), `== 2 -> 0x14` (rotation), `== 4 -> 0x10` (scale, `0x00471db5`). Returns a **normalised t** between two key frames, and **returns 0 when the frame precedes the first key**, so the engine skips the track rather than clamping to the first point. It finds the last key at or before the floored frame; on the last key (`0x00470be3`, equality included) it hands the sampler that key and the frame one after it (`param_5` 0, which the poser passes for position, `0x00471e9c`, and scale, `0x00471db3`; the rotation call passes 1, `0x00471c45`, and takes key 0 as the upper key there, `0x00470bee`, so a rotation wraps where the other two hold the last key), with t the frame less the last key's. **Every track whose last key is its clip's declared end reaches that arm on that frame, with t = 0** (1,201 of the 1,216 position tracks, in 455 clips, Q554): the straight sampler `0x00474bf0` then reads the point past the array times nought (finite floats there on all 421 straight such tracks), so the last key's own point comes out. A frame past the last key would read further (a Bezier wraps); no shipped clip reaches that, as no position track ends before its clip's declared span and the poser poses nothing past that span (its first test, `+0x1c` against `+0x20`) |
| `Route_SampleBezier` `0x00474840` | a plain cubic **Bezier**, not a spline | constants at `0x006fecb8` are 1, 3, -3, -6; expands to `P0 + 3(P1-P0)t + 3(P0-2P1+P2)t^2 + (-P0+3P1-3P2+P3)t^3`. The modulo wraparound is the **looping** arm |
| `Route_SampleStraight` `0x00474bf0` | the straight lerp | points `k` and `k + 1`, no wrap |
| `Route_BezierTangent` `0x00474a20` | the Bezier's first derivative | the same four points; what a route key's facing (`0x400`) turns by |
| `Route_StraightTangent` `0x00474c70` | a straight route's step | point `k + 1` less point `k` |
| `Anim_SamplePathPercent` `0x00474ea0` | a route key's percentage at a frame | the two frames' values blended; more than 50 apart (`0x006fece0`), the smaller is taken plus 100 (`0x006fece4` is -100) and the blend modulo 100 (`0x006fece8`). Its fifth argument is the route's type `& 0x1` (`0x00471f38`): closed (`0x00474ef3`), the frame after the last value reads `values[0]` (`values[(i+1) % count]`); open (`0x00474f0d`), the last value is read again. `PathTrack.Sample` always holds the last, which differs only for a fractional frame past the last on a closed route, and no shipped clip's span reaches one |
| `Matrix_FaceAlongTangent` `0x00470780` | a node turned to face along a tangent | rows: side = normalise(up x t), up = t x side, forward = t, where t is normalised before the call (`0x00472110`..`0x00472172`) and up is the node's second row |
| `FUN_00474cc0` | the third sampler (neither bit) | **used by nothing** |
| `FUN_004745c0` | the UV sampler | our sampler matches it line for line — **confirmed, do not re-derive** |
| `FUN_00471860` | the poser (`Anim_PoseTrack`) | assigns the node's **own local translation row** `node+0x40/+0x44/+0x48`; a node record's transform is a 4x4 at `+0x10`, so that row IS the translation, parent-local, exactly as a rotation key is. A model flagged `&4` at `+0x30` **adds** instead of assigning. Before a position track's first key (`0x00471ea3`, the search answering 0) the node keeps whatever translation it holds: the rest after a change of clip, but the last key of the clip's previous pass when the same clip comes round, which nothing restores. An additive (`&4`) model's nodes are put back every advance (`FUN_00473c70` calls `FUN_00472310` for each channel, `0x00473d0c`), so its point is added to the rest. Before a rotation track's first key the engine skips the track too (`0x00471c56`), so the node keeps its turn. **OpenTPW builds it** (Q554): `NodePoser`, rotation and position composed down the whole node tree for a clip with a position track. **Not the original's:** before a rotation track's first key `NodePoser` holds the first key; 79 of the 2,021 rotation tracks in the 455 position-track clips start past frame 0, four in Lost Kingdom (`volcanoe` node 1 at 55, `porkpies` node 37 at 10, `puzzlem1` and `puzzlem2` node 4 at 71), and in each of the four the first key, the last key and the node's rest turn are one turn, so neither a first play nor a clip coming round again shows it there. The additive rotation arm (`FUN_00470d10`) is counted, `ANIM_ADDITIVE_ROTATION`: one track in the 22 additive clips with a position track, and the 49 rotation-only clips (50 tracks) of additive models, which `MeshRotator` would pose as a replacement (the coaster pylons, Lost Kingdom's `coaster1`, `coaster3` and `minecart` among them); no source loads a pylon model, so those are dead by content |
| `0x00471c83`-`0x00471d32` | rotation-key easing | see below |
| `0x006febe4` | the easing segment constant **8.999995231628418** | deliberately under nine so `t == 1` stays in the last segment |
| `0x006febec` | the easing byte scale, 1/255 | |

**What those samplers run over, for a vehicle, is not a track but the model's own path table** — the
array at model file `0xac`, whose records name a run of 12-byte XYZ points. A Bezier route's point count
is a multiple of three rather than `3n+1`: a closed route's (type `0x1`) last segment runs back to point 0, which is what
`Route_SampleBezier`'s modulo arm above is wrapping, and an open one's last two points are never sampled (the three
vehicles' routes are open, below). The scalar that drives it is a separate per-frame array on an animation
track setting channel `0x200`, and it is a **percentage**: space's slide runs exactly 0 to 100 and the
haunted house 0 to 200, which is two laps. It does not always rise — the ferry runs its route backwards.
Layout and counts are in the FileFormats clone under Models (`*.MD2`); they are not repeated here. **OpenTPW** carries a node along its route both where it draws the model (`LobbyModel.Pose`) and where a script finds a
node on it (`NodePoser`, Q561): the route's point at the key's percentage is the node's translation within its parent.
In Lost Kingdom only the bus's, the ferry's and the seaplane's clips carry route keys (`Busm1`..`3`, `FerryM1`..`3`,
`Seaplanem1`..`3`, each on node 0), none of them with a position track; since Q583 a clip with a route track is drawn through
`NodePoser` too.

**Where a route key puts its node** (Q583; `Anim_PoseTrack` `0x00471ecf`..`0x00472192`). The node's parent must be a path
node (flag `0x8`); its path id is read at the parent's `+0x52` (`0x00471f27`). The key's percentage at the frame
(`Anim_SamplePathPercent`, only while the frame is within the track's start and start plus count) is taken plus 1000 modulo
100 (`0x006febf0` -1000, `0x006febf8` 100) and times the route's segments and 0.01 (`0x006fec00`); the whole part, `ftol`,
is the segment `k` and the rest `t` (`0x00471fc3`..`0x00471fe2`). **The segments are `points / 3` for a Bezier route and
`points` for a straight one, less one unless the route's type carries `0x1`** (`0x00471f9a`..`0x00471fbd`), so an open route
never runs back to its point 0: the bus's 45 points make 14 segments, the seaplane's and the ferry's 33 make 10. A Bezier
segment starts at point `3k` (`0x00471fed` passes `3k + 1`, the sampler reads one point before it). The point replaces the
node's translation within its parent. **With `0x400` the node also faces along the route** (`0x004720aa`): the tangent
(`Route_BezierTangent`, `Route_StraightTangent`) is normalised, the node's second row is set to (0, 1, 0) unless its track also
carries rotation keys (`0x8`, `0x00472148`..`0x00472186`), and `Matrix_FaceAlongTangent` writes its three rows. The
model's own turn of the node is thrown away while the clip plays. **OpenTPW builds it** (Q583): `LobbyModel.SampleRoute`,
`LobbyModel.RouteTangent`, `NodePoser` (`FacingAlong`, `RouteOf`), `AnimationFile.PathTrack.Sample`; the bus's sound node at
its two stops comes out at (529.2, 64.8) and (440.0, 65.2), where the original's bus voice stood, (529, 64) and (439, 65)
(Q562). Not the original's: a tangent of nought, which the engine divides by, leaves the turn as it was (no shipped route
has one at a key).

**A scale key sets the lengths of the node's three axes** (Q598; `Anim_PoseTrack` `0x00471d8d`, `Matrix_SetAxisLengths`
`0x0046f910`; the keys, FileFormats `models.md`, "Scale"). A track's channels go in this order: the turn (`0x8`, three fresh
rows over the matrix, `FUN_0046fbb0`), then the scale (`0x80`: each of rows 0 to 2 times the keyed length over its own
(`fdivr` at `0x0046f999`, with no guard for a zero-length row), so its direction is kept), then the route or the
position (`0x200`, `0x1`), then the facing along the route (`0x400`). Before the first key nothing is set. What hangs off the node is scaled with it, and the restore mask
`0x289` holds `0x80`, so a change of clip puts the node's lengths back. In Lost Kingdom the scale tracks that change a
drawn size are the construction clips' (`FriesC`, `SteakC`, `Cost_shpC`, `lookoutC`, `TourrideC`, `BirdC`, `Arc2x3C`,
`HyenasC`, `JunsprayC`, `supbogC`: the pieces grow out of nothing as a bought thing is built), the Hyenas' and the
Jungle Spray's water (`HyenasM1`..`M6`, `JunsprayM1`..`M6`, up to 14 times) and the Lookout's cogs (`lookoutM`, `S`,
under 2%) and `squarkC`'s arrow (node 3, `sq_arrow`, keyed down to 0.945); the rest key (1, 1, 1).
**Read in the running original** (`q598/orig`, `modelnodes.py --axes --poll` from the placing click, `scalecurve.py`): the Fries Shop's eight scaled nodes, 1,544 readings, each within 0.0031 of `FriesC`'s
keyed curve (the basket from 0.0232, 0.0079 to 1, squashed to y 0.4842, then 1); the Laughing Hyenas' twelve within
0.0011 of `HyenasC`'s (y from 0.0021); the Super Toilet's sign within 0.0010 of `supbogC`'s (0.1218 to 1).
**OpenTPW poses them** (Q598): `NodePoser.Pose` sets them after the turn, `NodePoser.Drives` takes any clip with a scale
track into the node tree, its restore and `LobbyModel.MeshesRested` put them back; the same three, and Jurassic Tours'
(not placed in the original: Q761), within 0.004 of the keys at each printed frame (`q598/run-final`, `run-ride-final`).

**Rotation keys carry an easing curve id** at key`+0x02` (`0xFFFF` = none) indexing an **8-byte** table at track descriptor `+0x34`, remapped through a **9-segment** ramp: **457 of 1,166 clips carry one**. Three things that matter: the id belongs to the **lower** key of the pair; the id is **not** the key's own index (**3,070 of 12,428 eased keys disagree**, ids reach 100, so the table is indexed not walked); and the ramp's endpoints are **implied** (0 before the first byte, 1 after the last), which is what makes it nine segments from eight bytes.

**The restore mask `0x289` includes position**, so unlike visibility a clip's position IS put back.

**Confirmed, do not re-derive:** morph decode-then-lerp is right; the engine's morph key cursor lives in **shared clip data** and a stateless search is the safe divergence; every clip declares first frame 0, so "elapsed from 0" and "absolute frame" are the same number. The morph channel count is the target mesh's vertex count `+2` under both flag values (bit `0x2` set or clear), measured: 1,337 of 1,337 flag-`0x01` and 397 of 397 flag-`0x03` descriptors whose base model resolves (FileFormats `models.md`, "Vertex morph"; upstream look 2026-10-19, `verify-models.md` L2).

### A rotation key is the orientation inside the parent, not in the model

Confirmed against the data. A key replaces the mesh's **local** rotation; composing it against the mesh's *world* rotation throws the parent's turn away. **1,078 of the game's 2,592 rotation tracks target a mesh where the two differ**, so this is general.

**Every gate parents its doors to a root that carries no rotation**, where the two are the same matrix — which is why the lobby never showed it.

The proof is the construction clip, which by definition ends on the built object: Jungle Spray's ends with `fence` keyed 90 deg, `gun01` 180, `Puddle_01` 245, `tube_case01` 30 and `Bench` square — each exactly that mesh's **local** rotation, and each disagreeing with its world one.

### Node visibility: bit 0x10, set at runtime and never in the data

`FUN_00471860` applies an animation's visibility channel: it walks the entries **from the end**, takes the first whose absolute value is at or before the current frame, and sets 0x10 when that entry is `< 1` (**so a 0 entry hides**) or clears it otherwise. If no entry qualifies **the flag is left alone**, not defaulted to visible.

In the node walks (the picker's `PickMarch_MeetModel`, `FUN_0045d090`, and three others) the walk tests `0x10` **after** computing the node's matrix and then carries on into the children, so **a hidden node does not hide its children** — bit `0x20` is what prunes a subtree. `0x80000000` protects a node from animation visibility altogether.

### The per-clip hide list

The list lives at clip block `+0x1a` (count) / `+0x38` (pointer). **854 of 1,166 clips carry one, 12,872 entries in total** (859 of the 1,237 clips under `levels/` that carry a block, counted without the track-table check).

- **`FUN_00472d70` HIDES the incoming clip's list at clip start.** Each entry resolves by the same `idx < meshCount ? meshTable + idx*0xa0 : nodeTable + (idx-meshCount)*0x58` rule `ModelFile` uses, then sets flag `0x10` unless the node carries `0x80000000`. **Entries are NODE INDICES.**
- **`FUN_00472310` (`Anim_RestoreOutgoing`) CLEARS `0x10` over the outgoing clip's list**, then restores `0x289` nodes from the master by copying matrix rows `+0x10/+0x20/+0x30/+0x40` (a node's mask is the outgoing clip's channels on it less the incoming clip's, so a node both move on the same channel stays as the outgoing left it; `NodePoser.Rest`, Q554), with arms for `0x1000` and `0x10000` and no `0x20000` arm: it copies no visibility keys. **Before that, its loop over the outgoing clip's tracks clears `0x10` on every node one of them targets, whatever the track is (`0x00472486`, a `0x80000000` node apart; the hide list's own clear is `0x00472401`)**, so what a clip's visibility keys hid is shown again at the next clip on the channel, and the incoming hide list is then hidden (`FUN_00472d70`, `0x00473117`). The whole restore is skipped where the start passed `0x4` or `0x8`, the hide list where it passed `0x8` (`FUN_00472f60`). The seaplane depends on it: `seaplanem3` (the take-off) hides its five meshes from frame 396 of 400 and `seaplanem1` (the landing) has no visibility track, so the original draws it on every later visit (Q622, `q622/orig/d-six.png`). **The bind hides the list on an idle channel too**: `FUN_00472f60` calls the restore only where the outgoing role is not 12 and the caller's flags miss `0xc`, but `FUN_00472d70` wherever the incoming role carries a clip and the flags miss `0x8`, whatever stood on the channel before; a start of the clip already playing restores it over itself (outgoing and incoming one clip) and hides its list again. So a bought Fries Shop, whose one clip `FriesC` lists its mesh 10 (`chip08`) and has no track for it, keeps `chip08` away for good: the running original's placed instance reads `chip08` `0x41000111` (bit `0x10`) and chips 1 to 7 `0x41140101`, its light cache never written (Q658, `q658/orig/b/modelnodes.txt`). OpenTPW (Q658): each start of a clip is counted per channel with its caller's flags (`RideAnimations.Bind`, `BoundOn`), and the drawn model or node tree, at a start it has not seen, puts back (`LobbyModel.Rest`, `ShownAtRest`) unless `0x4` or `0x8` and hides (`LobbyModel.Hide`, `HiddenAtBind`) unless `0x8` (`RideAnimations.PoseChannel`); a queued clip's promotion binds by its queued flags, the in-view arm, as every model is posed every frame here. The engine also restores at a loop's restart in view (`0x00473840`..`0x00473848` call `FUN_00472f60` with the same role and entry and flags `1`, `| 8` only off-screen), which reaches `FUN_00472310` with the outgoing clip the incoming one; OpenTPW's restart passes `0x8`, so it shows and hides nothing (Q661; no Lost Kingdom thing loops a clip whose visibility track starts after frame 0).
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

Outside the opcode range the engine passes *"RSSE: Unknown instruction"* to its logger, and without the 0x80 byte *"RSSE: Bad instruction - missing p..."*; the logger `FUN_005da3c0` is a bare `RET`, so neither prints.

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

**Nine handlers write the budget, and all nine write nought** (every one of the 106 jump-table targets walked to its `RET`; `search_bytes` on the flag's address finds its six references): `CRIT_UNLOCK`, `ENDSLICE`, `WAIT` and `WAITABS` (`0x005538d7`, a rewind of two), `WAITANIM` (`0x00552a78`, `0x00552b0b`), `WAIT4ANIM` (`0x0055391a`), `TRIGWAITANIM` (only on a re-entry that is still waiting; its first visit, `0x005536a0`, rewinds WITHOUT a yield, so the same turn dispatches it again), and the two channel forms. **`WAITANIM_CH` and `TRIGWAITANIM_CH` rewind one word short**: `0x005532f7` subtracts 3 from a four-word instruction (and `0x005536a0` 4 from a five-word one), so the next dispatch lands on operand 0, refuses it as *"RSSE: Bad instruction - missing parameter ?"* (a string for the bare-`RET` logger, so nothing prints) and parks the script. Nothing ships them, nor `WAITABS`. **The critical flag has two writers among the handlers**, `CRIT_LOCK` and `CRIT_UNLOCK`; the scheduler's reset at `0x00551701`, the initialiser `FUN_00551600` and the save-state reader `FUN_005597a0` are the others, and neither of those two is in the handlers' callee closure.

**A script's death frees its records, and the trigger is a negative PC.** Every operand-fetch overrun stores `0xffffd8f0` into the PC at `+0x3c`, and the teardown `FUN_00558500` drains the effect list. **Running off the end of the body is the normal death path**, and it passes *"RSSE: Tried to read outside script memory"* to the bare-`RET` logger on the way — **so that string is not evidence of a malformed script.**

### The clock, the speed word, and WAIT

**The clock is milliseconds.** The chain is `0x00402d70` -> `0x00402f10` -> `0x004030d0` -> `0x004033a0` -> `0x005f5fa0` -> **`0x005f5f10`**, and that last one is the whole answer: it calls `QueryPerformanceCounter` and divides by a stored divisor, and **falls back to `timeGetTime()`** when the counter is unavailable. The divisor is the counter's frequency over 1000: the clock's set-up at `0x005f5e80` loads 1000 (`MOV ESI,0x3e8`, `0x005f5e87`), asks `QueryPerformanceFrequency` (`0x005f5ea0`) and stores frequency / 1000 at `+0x4` (`DIV ESI` `0x005f5ee5`, `0x005f5eea`), with `timeGetTime()` at `+0x8` and the starting count at `+0x10`; the reading is (count − start) / that + the starting `timeGetTime` (`0x005f5f57`..`0x005f5f62`) — milliseconds on both paths. `WAIT 3000` is three seconds.

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

**The engine's readings survive a load unchanged; OpenTPW's, copied raw, would not, so the build moves them (below).** `FUN_005597a0` reads the variable block raw and
writes no variable after it, and the load puts the `0x785970` clock back to the saved reading (`FUN_00415140` at
`0x00415193`, through `FUN_004031f0`: offset = saved - now), on the load path where `FUN_00414d40`'s third argument is
not 1 (the test at `0x004150ae`; only `0x0054f105`, game type 1, pushes 1, and `0x005ad04a` and `0x005ac735` push 2, so the Easymode
load restores it). The clock is integer milliseconds (`FUN_00402f10`, the
`QueryPerformanceCounter` count over frequency/1000, through `__ftol`). OpenTPW copies the variables raw
(`ParkRides.Resume`) and runs its scripts on its own clock (`ParkRides.Moved`), far behind a save's, so without the move below `deadline - now`
would stay positive until OpenTPW's clock catches the save's: about 80 minutes for Alexah's jungle saves (4.82M ms), 31.8
hours for one taken at the shipped park's clock. The shipped park keeps none. Of the eight holders in Alexah's jungle
`New Save.TPWS` and `autosave.TPWS` (`q174c/clockvars.out`), five write before they read again (`spider` 128,
`gokarts` 197, `tourride` 62/87, `incagod` 159, `porkpie` 125); three read the stale one first: `mumbo` 62 and
`monkey` 85, saved on the branch back into their boarding loops, would never start a go by the timeout, empty or not, until
a boarder writes a fresh deadline or the ride fills (the engine starts them 9.5 s and 7.3 s after the load); `giftshop`
59 and 84 would never play its idle `WAITANIM 2`, which nothing but the timer releases.

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

**`TRIGWAITANIM` (19) at `0x552c1a` — OpenTPW reproduces the model path, its raw re-entry included (`RideScript.AnimationOn`), and, as a declared deviation, steps over the model-less one counted rather than parking.** First visit (`+0xbc` is 0): trigger as `TRIGANIM` does, with its two writes carried inline rather than through `TRIGANIM`'s tail (`+0xa4` at `0x00552d9f`, `+0xa8 = 0xffff` at `0x00552da5`, on the model and the model-less path alike), then `0x553693` does `INC EDI` / `+0xbc = EDI`, so the mark is the **animation id PLUS ONE** and 0 means "not armed"; then `+0x3c -= 4` onto itself and return **without** `+0x98 = 0`, so the same turn re-enters it. Re-entry: with a model, `FUN_00473fb0( model, &role, NULL, 0 )` reads channel 0 — a **plain accessor**, not a "channel cursor": at `*(model+0x10) + channel*0x38` it writes `AnimID` (`+0x04`) through its second argument and `SubAnim` (`+0x08`) through its third, each only when that pointer is not null (`0x00473fc9`, `0x00473fd6`), and returns `Flags` (`+0x00`), with no bounds check and no refresh — then `INC` and compare the **role** against the mark. **The returned `Flags` are thrown away unread** (`MOV EAX,[EBP+0xbc]` at `0x00552d08`), so no pose-flag test lies on any `TRIGWAITANIM` path; among the handlers only `GETANIM` and `GETANIM_CH` test `0x4` (`0x00552e05`, `0x0055374e`). Equal -> `0x5535f4` clears `+0xbc` and falls through; not equal -> `0x5535d6` rewinds 4 **and** sets `+0x98 = 0`. **With no model the accessor call is skipped and the comparison is made against the RAW THIRD OPERAND**, so it parks for ever unless the raw op2 word equals op0 — which **none** of the 133 shipped uses satisfies (132 differ, 1 is a variable). `TRIGWAITANIM_CH` (`0x553494`, its `+0xbc` test at `0x55359b`) shares `+0xbc` and the same tails: `0x553693`, `0x5535d6` and `0x5535f4` sit in its handler, and `TRIGWAITANIM` jumps into them (`0x00552daf`, `0x00552d17`, `0x00552d11`), so the two share one decode rather than agreeing independently.

**`GETANIM_CH`'s operands are `(destination, channel)`**, not `(role, entry)` — **the opposite shape to every other `_CH` instruction.** Counting its first operand as a role inflates any role census. The corrected corpus figures are **627 distinct (item, role) pairs and 806 distinct (item, role, entry) references, all 806 resolving to a shipped file bar the eight known absences**, with the highest entry index any script asks for being 9.

**No opcode hides a mesh of the base model**: ADDOBJ/KILLOBJ/FADEOBJ and the LIMBO family act on the script's own object table, not on model nodes. Variable names like `VAR_LETMEON` and `VAR_LANE1` appear nowhere in the exe — **they are data**. There is no "lane" in the exe; `VAR_LANE1..3` are variables inside the item's own `.RSE` script. `TRIGANIM` uses channel 0 and the `_CH` opcodes take one, so a sideshow's several clips run at once on separate channels.

**Corpus:** `WAITANIM` 547 uses / 250 scripts; `LOOPANIM` 210/114; `TRIGWAITANIM` 133/56; `WAIT4ANIM` 170/75; `TRIGANIM` 74/59; `FLUSHANIM` 15/7. The `_CH` variants are rare (`TRIGANIM_CH` 63/6, `GETANIM_CH` 15/5, `LOOPANIM_CH` 1), `TRIGANIMSPEED` is 4/4 (one `Gates.RSE` per theme), and `GETANIM`, `FLUSHANIM_CH`, `WAITANIM_CH` and `TRIGWAITANIM_CH` are unused.

#### Where OpenTPW's animation state parts from the engine's

Decoded for Q174 by two read-only decoders, each put to a skeptic in Ghidra, and measured over the corpus by two walkers
written apart, which agree site for site: every script walked from word 0 with both arms of every branch and an exact
return stack, carrying the engine's `+0xa4`/`+0xa8` and OpenTPW's side by side. Five differences, and a sixth found by
Q174b's review. **Q174b built the first three and Q174c the fourth**, so OpenTPW now parts from the engine at 5 and 6 (and 7, Q642);
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
that OpenTPW does not keep, and which no saved channel carries; and the real time the engine's clock runs between its
restore and the first tick, through the rest of the load (not measured), which OpenTPW does not count. The scheduler's tick counter and next handle do not differ: the
module's header puts both back (`0x005598d7`), so each script keeps its one-in-eight turn phase across a load, and OpenTPW
puts both back too, from a module that read whole (`RideScriptScheduler.Restore`, called by `ParkRides`).

**Measured in the game for Q174c** (silent, the stock jungle park loaded under a lobby pause and stepped in frames of
1/60 s, `save/` unchanged; `q174crun.py`, every reading predicted first, 7 of 7 on each build). This build: at the load
the Belly Bounce stood at frame 45.4 of 90 and the Fountain at 48.8 of 50, looping; the Belly Bounce read 55.6 at tick
10 and 41.3 at tick 84, once round at 1.1, with no loop queued behind it in 48 readings over ticks 10 to 60; both cameras
held role 6 at tick 75 and were on role 4 at tick 84, 3.7 and 4.7 frames in. The build before: 4.1 and 3.7 at the load,
the loop queued again behind itself from tick 36, and the cameras on role 6 to tick 84 and role 4 only by tick 176.
Paused photographs of each pair differ in pose where the census says they should (`q174c/*-pair.png`).

**5. A start on an idle channel keeps a stale `0x2` or `0x4` in the engine** (the Flags row above), so its new clip
stands still, on its first frame under `0x2` and on its last under `0x4` (`0x004736bd`), where `AnimTimeControl.Start` clears both on every start and plays it. Two
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

**7. A drawn frame's moment lies past OpenTPW's last tick, between the engine's last two.** Both pose the clips at the
frame's own moment (the engine's snapshot `DAT_007b496c`, a float frame at `0x00473740`; OpenTPW's
`ParkRides.NowMilliseconds`, Q675), so both move a clip every drawn frame. But the engine's tick runs at the start of
its 31 ms and OpenTPW's at the end (difference 6), so in the ticks' own stamps OpenTPW's moment is a tick further on,
and its clips run a tick ahead of the Hot Pot boats' place, which is eased back between the last two ticks as the
engine's is (`ParkBumperBoats.Put`; said at `ParkRides.OnUpdate`). A clip that ends within the part of a tick is
finished before the next tick in both, so a `WAITANIM` or a queued trigger can pass a tick sooner than when the sweep
took the last tick's instant, and whether it does hangs on where the frames fall, as in the engine. The stock
park's runs differ from the first ticks on any build, and which guest leaves first varies with them: the bus was first
summoned at random for a leaver on 1148, 1094 and 1147 in `q561/run3`, `run4` and `run-main` (before Q675; `run4`'s
Person 38 on tick 974 is the one `q675/A` (`6c637b3d`) and `q675/F` (`8c9a01c1`) show) and for the arrivals on 1264 in
`q561/run-x1`, `q675/C` and `q675/X3` (`ec2e6638`), the rates unchanged
(`q675/C`, `F`: 3.92 and 3.89 sweeps a second, 31.4 and 31.2 scheduler ticks).

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
    ADDOBJ_EXT(9)   005520e8  the same, 5 operands - ADDOBJ hardcodes 1000 where this passes op4; only a
                              particle reads it: unless it is 1000 it is stored (low word) at the emitter's
                              +0x9e via 0x51ffd0 (sound types ignore it; what +0x9e is, is not traced - it
                              is not the life, +0x1c: fork watch 2026-10-16, verify-audio-fx.md C2)
    KILLOBJ(10)     005522c5  walk +0xb0; every record whose +0x18 == operand is stopped and freed
    FADEOBJ(11)     005523d0  the same walk; differs ONLY for a sound
    EVENT(13)       00552615  3 operands -> FUN_005573d0(frame, op1, op2, op3, 1000); handle DISCARDED

**The 28-byte record:** `+0x00` next, `+0x04` prev, `+0x08` type, `+0x0c` the spawn's handle, `+0x10` node, `+0x14` the node's index on the model, `+0x18` the tag. `DAT_008791b4` is a live count of them.

**`FUN_005573d0` is the whole subsystem**, switching on the type: **1-2 `Particles_Spawn` / `Particles_SpawnFull`; 3-9 `Sound_PlayEffect` through a named category** — 3 `cat_rides'`, 4 `cat_ambient'` (a SECOND pair registered by `FUN_0051ec50`), 5 `cat_rides`, 6 `cat_kids`, 7 `cat_staff`, 8 `cat_ambient`, 9 `cat_ui`, all from `Sound_RegisterGlobalCategories`; **10** the thing's own custom bank, which returns 0 early if none is registered. Anything else is **"RSSE: Unknown object type"**, and the guard is unsigned after a `DEC`, so 0 and negatives take it.

**The no-model path does not do nothing — this is the opposite of the animation family.** `FUN_00556b90` is the position gate, and with the model handle at `+0xc8` nought it **returns 1**, not 0, having written position `(0, 0, 0)` (the z is the float at `0x00700fd8`, which is 0.0). Its **only** `return 0` is "RSSE: Invalid Node ID", reachable only when a model exists. **So the engine really does spawn, at the origin.** Two further traps in that gate: it **never writes the direction vector** on the fallback path, so type 2 with no model reads **uninitialised stack** in the original; and the same block also serves a live case (model present, node negative) where it returns a **real** position.

**`KILLOBJ` kills EVERY match, not the first.** At `0x552376` it does `MOV EAX,ESI / MOV ESI,[ESI]` — advancing *before* unlinking what it matched — then rejoins the test at `0x5523bd`. There is no break anywhere in it, and even the unknown-type error path converges on the same unlink-and-continue. **`FADEOBJ` is identical for a particle** (both reach `FUN_0051ff70(handle, -2)`, which is `Particles_Kill`'s own body) and differs only for a sound: `Sound_Stop` vs `Sound_StopFading`.

**`EVENT` keeps no record at all**, so nothing it starts can ever be stopped — that asymmetry is the only difference between it and `ADDOBJ`, and why `EVENT` has no fourth operand.

**The effect id is NOT range-checked.** No shipped script names a particle above 95, but `FUN_005573d0` tests the id for **bit 15** and remaps it through `FUN_004145d0`. A corpus range is not an engine invariant.

**The record is a live binding, which the handlers alone do not show.** `FUN_005516b0` walks the `+0xb0` list **every tick, for every script**, due a turn or not (a script not due jumps from `0x5516f6` straight here): `0x55188f MOV EDI,[ESI+0xb0]`, advancing at `0x551ab2`, dispatching per record on `+0x8` through a byte map at `0x551b18`. For each it **re-reads the model at `+0xc8`, re-resolves the node through the cached index at `+0x14`, and pushes the position back into the particle or sound system through the handle at `+0xc`.** So `ADDOBJ` is not "spawn and keep a handle" — the record is what glues a playing effect to a moving ride, and that is the shape any world-side adapter must take.

**Other writers of the `+0xb0` list**, so nothing here reads as ADDOBJ's private property: `ADDOBJ_EXT`; the **save-state reader** `FUN_005597a0`, which rebuilds it from an `"OBJ "` section (no shipped `.RSE` has one — they all begin `RSSE`); the teardown `FUN_00558500`; and `FUN_005516b0` itself.

**A tag is a GROUP label, not an object id.** 95 of the 308 files give one tag to several `ADDOBJ`s — `AnimCtrl.RSE` (and its hallow/jungle/space twins) uses tag **1 at 24 separate sites**, `ccride.RSE` tag 10 six times. So `KILLOBJ`'s kill-every-match is the ordinary case, not a corner. **Tag 0 is legitimate data** and must never be a "no tag" sentinel — and the engine's own overrun path synthesises operand 0 (`XOR ECX,ECX` after the `0x765ca4` complaint), which then kills every record tagged 0.

**Two defects not to copy, and one limit not to invent.** The node cache at `+0x14` is used as an array index with **no test for its own `0xffffffff` failure value**, so a non-negative node id the model does not have reads out of bounds. **Nothing ages or reaps a record** whose effect has finished, so a record can outlive its effect indefinitely while the tick loop keeps pushing positions into a dead handle — the original survives only because a handle is slot-plus-generation (slot max `0x77`) and every consumer validates it. And the global `DAT_008791b4` has **no cap anywhere**: every read is followed by an `INC`, a `DEC` or a return, with no comparison against a maximum.

**Unexercised by shipped content:** `ADDOBJ` never uses type 6 or type 10, and `EVENT` never uses 7-10, so the whole custom-sound-bank path (`FUN_004145e0`) is decoded but untested by any shipped script.

**Corpus:** `ADDOBJ` 644 uses / 164 files, `EVENT` 527/107, `KILLOBJ` 235/122, `FADEOBJ` 113/39, `SETOBJPARAM` 20, **`ADDOBJ_EXT` and `EVENT_EXT` 0 uses**. Node `-1` is the commonest operand in the whole corpus (277 of 644, 341 of 527). Tags run 0-1000 and two tags are killed that no `ADDOBJ` creates. Particle types use ids 1..95; sound types 6..220. **Five shipped requests name an id their category lacks**: jungle
`Speaker2`, `Speaker3` and `Speaker4` run `ADDOBJ 4 -1 6 1` (jungle's `cat_ambient` holds 177-182 and 190-192), and
jungle `incagod` (word 332) and hallow `bumper` (word 188) run `EVENT 3 -1 43` (no theme's own `cat_rides` has 43; the
global `cat_rides` has 43 and the global `cat_ambient` has 6). **Each is silent, with no fallback**: types 3 and 4 play
in the level's own pair (`DAT_00803a3c`, `DAT_00803a38`), a category apart from the global pair of the same name; the
play (`0x006b87d0`) looks the id up in that category's 20-byte records alone (`0x006c5870`), and an absent id, or one
with no variations, returns the caller's handle, 0 for a script, and tries no other category. The `ADDOBJ`'s 0 then
frees its record (below).

**`ADDOBJ` hands its record to `FUN_00557970`** (`0x005520d5`), which spawns through `FUN_005573d0`, keeps the
answer at `+0xc`, looks the node up for `+0x14` (`FUN_0044b220` under `0x100` for types 1 and 2, `0x200` for 3 to
10) and writes the tag last; **where the spawn answered nought it unlinks and frees the record instead**. The
whole record, and how a park file holds the list, is `saves.md`, the same section.

**A sound record is moved on its script's eighth tick, a particle on every one.** The walk's byte map at `0x00551b18`
sends types 3 to 10 to one arm (`0x00551a1c`) which acts only when the script's id against the tick, `( [frame +0x8] ^
[0x008791a4] ) & 7`, is nought, the test a script's turn takes (`0x005516e9`): it reads the node's stored matrix
through the cached index and hands `FUN_0051c270( handle, x, y, z )`, a set of the voice's place, the translation cut to
whole numbers, or (0, 0, 0) for a record with no matrix (`0x00551a66`). A record of no node is never moved, by either arm
(`0x005518a0`). **`KILLOBJ` stops a sound with `Sound_Stop` (`0x0051c2c0`), `FADEOBJ` with `Sound_StopFading`, and the
script's end (`FUN_00558500`) every record's sound with `Sound_Stop`** and every particle's with `FUN_0051ff70( handle, -2 )`.
What OpenTPW plays of it is `audio.md`, "A script's sounds".

### `SETOBJPARAM` (12) and `DIPMUSIC` (104) — both buildable with no world

`SETOBJPARAM` at `0x005524d9` is `<tag> <param> <value>`, walking **`+0xb0`** — the same record list `ADDOBJ` fills — and matching the record's tag at `+0x18`. Type dispatch (byte map `0x5569c4`, jump table `0x5569b8`) has **two cases only: particle types 1-2 do nothing**, sound types 3-10 call `FUN_0051bc40(record+0xc, param, value)` (`Sound_SetParameter`) and store the answer back at `+0xc`: the handle itself while the voice lives, nought otherwise (`audio.md`, "The music's class of a script"). **That call returns 0 when the sound system is down** (`DAT_00802bc8`/`DAT_00802bd4`, written only by the sound init and its teardown), so with no world the record's handle becomes nought — the engine's own answer. All 20 uses are in `bus.RSE`, **one copy per theme — four scripts, not one** — as `(1, 20, 0)` x12 and `(1, 20, 75)` x8. It is a **third reader** of the list and rewrites `+0xc`, the handle itself. **Its byte table is `[0,0,1,1,...]`, NOT `KILLOBJ`/`FADEOBJ`'s `[0,1,2,2,...]`** — do not carry the one over to the other. It is a no-op on a particle record. The handler has not been read in full.

`DIPMUSIC` at `0x005564c0` resolves its operand, stores the low byte at frame **`+0xb9`**, and calls `FUN_0051e710`, which is only `DAT_00803ac8 = 1; DAT_00803ad0 = value`. **The frame byte is its lifetime**: `FUN_00558500` tests `+0xb9` and calls `FUN_0051e710(0)`, so a script's death un-dips the music. All 8 shipped uses pass a literal 1.

### `REPAIREFFECT` (93) at `0x005562a6` — unbuilt; it falls to the counted dispatch default

One operand, shipped **70x value 1 and 70x value 0**. It does **not** block — no rewind, no `+0x98`, and its only unusual exit is the `NOP` handler — so this is a different failure from `TRIGWAITANIM`: **it would take an access violation rather than park.**

    FUN_00556de0(frame, value)   value==0 -> if +0xcc, stop that particle and clear +0xcc.
                                 value!=0 -> GATED on the thing handle +0xac != 0; looks the thing
                                 up, takes its particle id at +0x68, resolves a position from the
                                 model at +0xc8, Particles_Spawn, stores the handle at +0xcc.
    then value!=0 (0x5562cd)     UNGATED: thingTable[+0xc8] -> FUN_00556a80 -> Sound_PlayEffect
                                 twice, sample ids 0x6f (111) and 0xd3 (211).
    then value==0 (0x55635f)     needs +0x8c > 5 (the script declares at least six variables), reads
                                 VARIABLE 5 (VAR_ONRIDE), then UNGATED as above -> FUN_00551320, the
                                 SINGLESCREAM picker called with (VAR_ONRIDE, 1, 1): step (1+1)/50 = 0, so
                                 75, 79, 83 or 87 (0x4b, 0x4f, 0x53, 0x57) by the number aboard, silent with
                                 none (fork watch 2026-10-16, verify-audio-fx.md A1).

**`+0xcc` is the repair particle's handle** — a frame field, one slot, and `FUN_00558500` clears it on death. The killer is `FUN_00556a80`: it does `*(*(*(arg + 8) + 4) + 0x78)` with **no guard**, and its argument is `thingTable[frame+0xc8]` at `0x7a4610` — a table filled at runtime, indexed by the script's model handle with no guard. The sound side is safe (`FUN_0051bfc0` is `Sound_PlayEffect`, guarded on `DAT_00802bc8`/`DAT_00802bd4`), but that does not rescue it.

### The lights (82-85) — decoded, unshipped

`ENABLELIGHT` `0x00555c7a`, `DISABLELIGHT` `0x00555ccd`, `SETLIGHT` `0x00555d20`, `COLOURLIGHT` `0x00555d9a`. Each
looks operand 1 up with `FUN_0044b220( thingTable[frame +0xc8], 0x20000, node )` (`0x7a4610`) and passes the index
to `FUN_004587b0( thing id, index )` for the light. `ENABLELIGHT` calls `FUN_00458910`, which clears bit `0x10000000`
of the light's `+0x50`; `DISABLELIGHT` calls `FUN_00458940`, which sets it. `SETLIGHT` multiplies its second operand
by the float at `0x00700fe0` (0.01) and calls `FUN_004587e0( light, f )`, which scales the light's base colour by it;
`COLOURLIGHT` calls `FUN_00458890( light, r × 0.01, g × 0.01, b × 0.01 )`, which writes the colour. **None checks for
a missing node**: `FUN_0044b220` answers -1, and `FUN_004587b0` then answers the word 12 bytes before the table (entry -1's `+0x8`, `0x004587c5`..`0x004587d2`), which the callee follows three deep and writes through (`FUN_00458910`, `0x00458915`..`0x00458930`); what lies before the table was not traced, so very likely a
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
- **`0x2`** (`TEST BL,2` at `0x00559102`), for the script named in the call only, and only when its thing word `+0xac` is set: the item's `Info.DestroyParticleEffect` (descriptor `+0x64`) is spawned by `Particles_Spawn` (`FUN_00521e60`) at the centre of the thing's cell rectangle ×10 at its base height, and its emitter's area is set to the box (`FUN_00466b70`, its heights from the `.hmp`) by `FUN_00520030` — emitter `+0x44/+0x48/+0x4c`, which is the template's `Area`. With no model it spawns at the origin (`0x00559170`). The value is the item's own where it declares one and its category's otherwise (FileFormats `sam.md`, "The particle effects an item gives off"); 0 spawns nothing (`0x005590f7`), and every value the jungle's items use, 75 to 78, is a world effect (`OnScreen` 0 in `Tp2.plb`).
- **`0x4`** (`0x00558582`, `0x005585f8`, inside `FUN_00558500`): the heads `ADDHEAD` hung on the model are taken off it (`FUN_0044b220` → `FUN_0044b4c0`, which frees the head's render object), and so are those of walk slots in action 4, state 2. The peep is untouched.
- Bit `0x1`, and anything above `0x4`, is read nowhere.

**Whatever the mode**, inside the `+0xac` block: a sound-engine zone at `+0xd4` is released by `FUN_0051c6b0` and cleared. The loader makes it at `0x00558fda`, only when the item's `Info.CreateParticleEffect` (`+0x60`) is non-zero; the save-state reader makes one too (`0x00559f0a`). What the zone does to the sound is not decoded.

**`FUN_00558500`, the flat destructor, releases in this order** (`0x00558500`..`0x005587cd`): the music dip (`+0xb9` → `FUN_0051e710(0)`); the TOUR ride record when `+0xc8` is set (`FUN_0055d3d0(+0x9c)`, whose head detaches ignore the mode, and which kills each car's particle, frees its model and stops its sound); the repair particle (`+0xcc`, `FUN_0051ff70(h,-2)`, cleared); the scream (`+0xd0`, `Sound_StopFading` with fade `0x3c`, **not** cleared); the ADDHEAD array `+0x30` and the walk slots `+0x2c` as above; the live count `DAT_008791ac`; the registry link; the body, variables, stack, limbo slots, bounce slots, walk slots, head array, strings and directory; and last the `+0xb0` effect list — particle types 1-2 by `FUN_0051ff70(h,-2)`, sound types 3-10 by a plain `Sound_Stop` (not a fade), `DAT_008791b4` decremented per node. **It releases no peep**: limbo, bounce and walk storage is only freed. The destructor's "object removed" message has already let everybody go — see `park-engine.md`, "Selling and the people on it".

**Two writes through NULL**: where the parent id names nobody, `MOV [EAX+0xc],EAX` with `EAX` = 0 at `0x005592f3`, and on the empty-registry arm at `0x00559316`. **`FUN_005da3c0` is a single `RET`**, so every log and assert string this family passes it is inert.

**OpenTPW** takes a sold thing's script down through `ParkRides.Unbind`, the scheduler's `Destroy` with mode 7's meaning: the particle is counted (`DESTROY_PARTICLE_EFFECT`), and `0x4` has nothing to undo because nothing hangs a head.

### What building a thing gives off

Q663. The yellow and green sparkles a bought thing rises among are its item's `Info.CreateParticleEffect` (descriptor
`+0x60`; FileFormats `sam.md`, "The particle effects an item gives off"), started by the **script loader**
`FUN_005587f0` as the new thing's script is made, not by its script and not by the build path `FUN_00463060`.

| Address | What it does | Evidence |
|---|---|---|
| `0x004dd070` | The object's script start `FUN_004dcf90` (from `Object_Construct`, `0x004db517`) calls the loader with the thing's id and a third argument of `DAT_00785a2c`, which is 1 only while a loaded save's layout is replayed | Decompiled |
| `0x00558e12`..`0x00558e2b` | The block runs only when that argument is nought and the script's thing word `+0xac` is set | Listing |
| `0x00558e5d`..`0x00558e81` | The item record (`FUN_00412e90` on the thing's `+0xe` item id, `[0x007cfb90 + id x 0x14]`); its `+0x60` nought spawns nothing | Listing |
| `0x00558e87`..`0x00558ea5` | It needs the script's model (`+0xc8`) and that model's box, `FUN_00466b70` (`ride-operation.md`: the cells `+0xc0`..`+0xc4` x 10, the heights from the `.hmp`); with none, nothing | Listing |
| `0x00558eab`..`0x00558f25` | `Particles_Spawn( effect, x, y, z )`: x and z the box's middle, y the model's base height `[[[rec + 8] + 4] + 0x78] + 0x44`, each x 1024 (`0x00700fd0` 0.5, `0x00700fdc` 1024.0) | Listing; constants read |
| `0x00558f3d`..`0x00558fac` | `Particles_SetArea( handle, dx x 512, dy x 256, dz x 512 )` (`0x00701050`, `0x00701054`): the emitter's area `+0x44`/`+0x48`/`+0x4c`, each over 16, so half the box across and along and a quarter of its height, in place of the template's | Listing; `Particles_SetArea` decompiled |
| `0x00558fda` | `FUN_0051c640( ... )` makes a sound-engine zone about the box's middle, kept at `+0xd4` and released at the teardown (above). What it does to the sound is not decoded | Listing |

The place uses the model's own cells, `+0xc0`/`+0xc4` (the rule Q744 names for the build marks), never the anchor's
footprint. **Gaps the decode leaves:** an item with no `.RSE` falls back to `Data\TestScript\test.rse`
(`0x004dd031`), which the game does not ship; whether the loader then makes a script (and the effect) was not read.
`SPAWNCHILD`'s two calls of the loader (`0x00555123`, `0x0055524d`) both pass a third argument of 0 (`push eax` after `xor eax,eax`, `0x005550e6`, `0x00555210`) and a thing word of 0 (a slot pushed at `0x00555115`, `0x0055523f`, turned into the word by `0x00409cd0`), which the loader stores at `+0xac` (`0x00558d2e`) and tests at `0x00558e2b`: a child script never starts the effect.

**Measured in the original** (`q663/orig/a`, `late30.TPWS`, a Round Fountain bought and placed by a click, predicted
first). One emitter started on the tick after the click: effect 81 (`Create3`, the fountain's own value) at (495, 0,
355), life 30, rates (1, 1, 1, 0), most 146 at density 500 (`original/emitters.py`); the fountain's box read
(48,34)..(50,36), middle (495, 355), base height nought (`original/modelboxes.py`). `brk.sh` on `Particles_Spawn` named
the caller `0x00558f2a`. The emitter was gone about 1.5 s later, the build still rising (`orig-build-strip.png`: the
sparkles in the first two frames, 0.4 s apart). **A load starts none:** the loader is reached at a
load, for the gates (`0x00515be1`) and the lights (`0x00515c94`), which the park's init `FUN_005156a0` builds through
the constructor `0x004db090` with a third argument of 0, and starts nothing because their items declare effect 0
(`0x00558e81`); the rest of a park's things come back through the save-state reader, which does not reach it. Two loads
watched from before their click (`q703d/orig/a-emit.log`, `q703d/fs/g-emit.log`) name no effect 79 to 82; their
sampling is 0.75 s against a life of 1.2 to 1.6 s, so they are a weak check beside the code. **A move starts it again:**
the put-down after a move builds the object afresh through the same constructor (`0x00525417`, `FUN_00528a70`;
`park-engine.md`).

**OpenTPW** reads the value (`ItemDescriptionFile.CreateParticleEffect`, the item's own over its category's) and counts
it where a bought or moved thing's script starts (`ParkRides.BindNew`, `CREATE_PARTICLE_EFFECT`, with a log line naming the
effect); nothing is spawned. The load path does not reach it. Building it is Q683.

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

**`INITIALISE` binds the script to its ride.** `FUN_0043b050` walks a linked list from `DAT_00790fe0`, matching `node[+0x24]` against the **script's own id** (field 2, the same id the scheduler uses for `(id ^ tick) & 7`), and returns `node[+0x14]`. That handle is kept at **`+0xe0`** and ops 1-6 pass it (op 7 never reads it). With a null handle `INITIALISE` skips its speed push (`0x00554aa5`); ops 1-6 do not test it and index `DAT_00790bd0` with it regardless (`FUN_0043b1f0`, `0x0043b1f4`). It then pushes the script's speed word into the ride via `FUN_0043b2b0`, the same call the execution loop makes each turn.

**`COAST 2 0` is idiomatic, not a bug — and it proves the whole model.** `Coaster1` word 44 is `COAST 2 0` followed at 47 by `BRANCH_Z`. The literal `0` is a **deliberate dummy destination**: the room remaining lands in the result register, the store is silently skipped because the destination is not a variable, and the branch tests the register. So the "ignored write" path is a **feature the scripts rely on**, which is why an interpreter must skip rather than throw.

**Only 12 scripts use `COAST` at all**, and every op count is a multiple of 12 — the coaster family is near-identical across the four themes.

### `BUMP` and `TOUR` — a different object family

Dispatch at `0x005546f5`: `DEC EAX` / `CMP EAX,0x10` / `JA 0x00554c25` / `JMP [EAX*4 + 0x556a18]` — **17 entries, selectors 1..17**, table immediately BEFORE COAST's at `0x556a5c` (over-reading BUMP's table by three entries returns COAST's `00554ac7`/`00554af5`/`00554b39`, a free corroboration of the COAST map).

    sel  1 00554726   sel  5 00554836   sel  9 00554895   sel 13 0055495e
    sel  2 00554766   sel  6 005547f6   sel 10 005548da   sel 14 00554996
    sel  3 00554816   sel  7 005547d6   sel 11 005548fa   sel 15 *** ERROR HANDLER ***
    sel  4 005547b2   sel  8 00554850   sel 12 0055493b   sel 16 005549c8 / sel 17 005549ed

**Selector 15 is invalid in the binary** — its table entry is the error logger itself — **and the corpus confirms it: 199 uses across 12 scripts, selectors 1..14/16/17, and ZERO uses of 15.** A falsifiable prediction from the binary that the data upheld.

**Both prologues make the same call, and only BUMP keeps its answer.** Each reads the 16-bit field at `[EBP+0xac]` and looks an object up by it (`CALL 0x004cd300`, which answers the dword at `0x007cfb90 + index × 20`; the `this` both load from the global at `0x007cf6ec` is never read); BUMP keeps the answer in `ESI` (`0x0055470b`), where COAST's operand fetch overwrites it (`0x00554a6f`). BUMP's handlers pass **`[ESI+0x28]`** into functions in `0x00544xxx`-`0x0054axxx`, except sel 5, which reads `[ESI+0x2c]`, and sel 17, which passes `ESI` itself. COAST instead uses the `+0xe0` handle INITIALISE stores, indexing `DAT_00790bd0`, with functions in `0x0043bxxx`. **Two unrelated object families** — so one "ride" serving both would be an abstraction the original does not have.

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
| 8 | `FUN_00544c80` / `FUN_00544e50` | Non-zero: `+0x54` = 2, smoke (effect 2, its handle at the car's `+0x2c`) at the emitter node of each car flagged `0x4000` that has none, Hot Pot cars anim `0xc`. Zero: `+0x54` = 0, and only from 2 "Ride Fixed", smoke killed, anim 5. `saves.md`, "OpenTPW's writer, a smoking car". |
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
buoys instead (`FUN_005474b0`'s buoy arm, "GoKart Race Over"): "The karts' and the water ride's track, buoys and
race", below.

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
four corners at the car's radius along its heading each take a height from the scene under them ("The scene's
height under a point", below), plus, for flag `0x1000000`, a bob of 0.00125 (`DAT_00700ef0`) × the sine
of `+0x90` × 6, 4, 3 and 5, eased across the frame; the car stands at their average, pitched and rolled by their
differences, turned `((heading - 0x100) & 0x1ff)` 512ths of a turn; its wake (flag `0x2000000`) trails it by its speed,
0.7 higher. **A rider is attached to the car's seat node**, found as `0x80` and its seat id (`FUN_0044b220`) and
attached by `FUN_0044b410`: the `b_car`'s one
is `Head1` (id 1, flags `0x100000b1`); a second rider of a car has no node and is still counted seated.

### The scene's height under a point

Read for Q257s, first-hand in Ghidra, after Q257r wrote a boat's height 0.2 above the original's. The bumper draw
asks for it under each car's four corners; a walk slot without flag bit 0 asks the same routines
(`ride-operation.md`, the slot's `+0x1a`). Three calls.

**Whose model** (`FUN_00450ac0( cellX, cellZ )`, each clamped to the landscape's size). A cell of the track grid
holding a piece answers that piece's model (`FUN_0053bf30`). Otherwise `FUN_00527e80`: a cell on the entrance's row (`DAT_007855c0`), from one cell before `EntranceA`'s x (`DAT_007855bc`) to one past `EntranceB`'s (`DAT_007855c4`), answers the thing the world keeps at `+0x1da732`, whatever the cell holds; any other cell goes to `FUN_00527d60`, where a cell of type 4, 9 or 10 (a
footprint, an entrance, an exit) is followed to its **parent cell** (`+0x2a4`), and the first object on that cell's
thing list (`FUN_004d99e0`, thing type 3) gives its model handle `+0x20`. Any other cell answers nought, and the
caller keeps what it had: the bumper draw keeps the car's `+0xa0`, the height it last stood at.

**Which way** (`FUN_00450ea0( model )`, kept in `DAT_0079fb10`):

| Mode | When | The height (`FUN_004511a0`), each plus the model header's float `+0xa0` |
|---|---|---|
| 2 | the model's lookup records' shared flags carry `0x8`: some record's file flags carry `0x2` | the highest face under the point of the meshes whose record carries `0x2` (`FUN_00451290`, `FUN_00451390`): only faces whose first word carries `0x8000`, the point inside all three edges seen from above, the height from the face's normal; a record with `0x4` ends at its first face. The vertices are the rest pose's, moved by each node's translation chain and the object's place and quarter turn (`FUN_00450f20`): no clip moves them |
| 1 | else the header's flags `+0x30` carry `0x80`, or its float `+0xa0` is not nought | the landscape's under the point (`FUN_004527f0`) |
| 4 | else | the one mesh the model's instance keeps at `+0x2c`, with the normals at `+0x30`; a miss answers the root node's own height. Not read: who sets `+0x2c` |

A miss in mode 2 answers -100 (`0xc2c80000`) plus the header's float like any other answer; the bumper draw replaces an answer of exactly -100 with the car's `+0xa0` (`0x0054675a`), so only where that float is nought. A float at
`+0xa0` that is not a number is set to nought first (`FUN_0067b410`).

**The landscape's height** (`FUN_004527f0( x, z )`, world units, a cell 10): nought off the map. On a cell whose
flags lack `0x800`, `near + (alongX - near) × u + (alongZ - near) × v`: the plane of the near corner and its two
neighbours across the whole cell, the far corner not read. With `0x800` the cell is two triangles, split from the
near corner (`v <= u` takes near, alongX, far) or, with `0x4`, along the other diagonal. **No cell of the four parks
carries `0x800`** (32,640 counted, 1,743 of them not flat), so those two arms are dead by CONTENT.

**The Hot Pot** (`bumper.MD2`): no record with `0x2`, no face with `0x8000`, and `+0xa0` = **29.8**. So a boat's corner
reads the ground under it plus 29.8, and the water mesh is never asked: its top stands at about 30.005. Measured
over all 2,129 models (`md2surf.py --sweep`, FileFormats `models.md`, "The surface a point stands on"): 83 static
models carry the float, 47 lookup records carry `0x2` (each with `0x4`) in 32 models, and all 2,182 faces with
`0x8000` are on those meshes.

**OpenTPW** (`ParkSceneHeight.Under`, `HeightfieldFile.ScapeHeight`, `ModelFile.SurfaceLift` and `HasSurfaceMeshes`,
`ParkBumperBoats.Float`). Mode 1 by the float is built, and a boat stands at the average of its four corners'. A
model with a `0x2` record is counted and keeps the last height (`SCENE_HEIGHT_SURFACE_MESHES`), as is one with
neither (`SCENE_HEIGHT_OWN_MESH`); a track piece's cell is read as any other cell, since no car here drives on one;
header flag `0x80`, which no file stores, is set at run time by the loader (`0x004625f4`, loader flag `0x40` from load bit `0x2`) on every kart and water track piece (`0x10422`), queue piece (`0x12423`, `0x112423`) and boundary-hoarding piece (`0x422`); those models take mode 1, the landscape's height, except the hill pieces (`gk_trckH`, `gk_trckH_d`, `gk_trckH_u`, `wr_trckh`, `wr_trckh_d`, `wr_trckh_u`), which carry a `0x7` lookup record (`[model+0x7c]`, 20 bytes each) and so the instance flag `0x8`, which `0x450ea0` tests first (`0x450eb1`, before the `0x80` test `0x450ecf`): mode 2, draped faces, set by the loader `0x44a870` when any lookup record carries `0x2` (`0x44a9ed`..`0x44a9f2`; the flat pieces `gk_trckB`, `B2`, `Q`, `S`, `v`, `X` and `wr_trckb`, `b2`, `s`, `s1`, `x`, `x2`, and `gk_trckH_a` and `wr_trckh_a`, take mode 1). It is not read here (fork watch 2026-10-25, `verify-all.md` N2; corrected fork watch 2026-10-28, verify-world C1). The corner that keeps the last height keeps the boat's, nought
at its first drawing, where the original's is the car's `+0xa0`, which a load fills from the file.

**Confirmed** (`q257s/confirm.py`, on the desktop; `PREDICTION.txt` first). A Hot Pot bought on (41,23), four boats in
a go, paused: `bumpers` read `height 29.6561` on all four at phase 596, the file's `+0xa0` the same, and less its
bob 29.7986 to 29.8011 (the frame's easing is between the two); the original's own three files read 29.7993, 29.7994
and 29.7958. No gap counted. **The control** (the build before): 29.9475, less its bob 30.0018 to 30.0081. **The
original under Proton** (`loadfile.sh`, `q257r/orig/cars.py`, `q257s/orig/a-load.log`): after the list's click the
four cars stood at the file's places, timer 666, riders 33, 44, 45 and 46, and its own `+0xa0` read 29.6573 at the
file's phase beside the file's 29.6561; Q257r's file was 0.2053 off. Its frame and OpenTPW's both show four boats in
the water (`orig/a-2-loaded.png`, `pot-fix-left-control-right.png`); 0.2 of a unit is under what either frame
resolves, so the height is the census's and the file's, not the picture's. **Predictions wrong, mine:** that the Hot
Pot would have a `0x2` mesh 0.2 under its water (it has the float instead); that under 20 models carry the float (83);
that the control would read 30.000 (30.005); and the first build looked for the object on the corner's own cell, not
its parent's, found none, and sank every boat to -82 in the first game run. **Not run:** a pot on sloping ground (the
stock ground under it is flat, at nought), a boat with a corner off its ride's cells, either other mode.

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
but the water family's turns toward its velocity's heading at the same rate (the water family's `+0x58` is a spin:
"The karts' and the water ride's track, buoys and race", "The step"). `+0x54 = ( +0x54 + +0x5c ) & 0x1ff`. Thrust is at most 9 an axis
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
car, so it never counts. `Bumper_PushOffObstacles` acts on objects flagged `0x40`: the unused type −2's, and the
inner circle of every bend and crossing of the karts' and the water ride's track ("The karts' and the water ride's
track, buoys and race", below).

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

### The karts' and the water ride's track, buoys and race

Read for Q581, first-hand in Ghidra (the decompiles are kept in `q581/`), and checked in the original under Proton on
`q581/karts.TPWS` (`q495/cash1500.TPWS` with the bank at 60,000 and both rides researched) with `original/trackride.py`
(`q581/PREDICTION.txt`, rounds A to D). Dino Karts (1150) is BumperType −4 and `Bumper.WhichTrackType` 1, Splish Splash
(1160) −5 and 2. Every arm below takes the other themes' karts (−7, −9, −10) with −4 and their water rides (−8, −12,
−13) with −5. North is −z, the direction ring's (`park-engine.md`).

**Bought.** The place commit (`FUN_00524960` from `0x00524d8c`) lays the footprint, and for track type 1 or 2
`FUN_0052a050` the queue's first cell and `TrackRide_LayStation` (`FUN_0052a1a0`) the station: a run of track type 0xd
(karts) or 0x12 (water) over the station's two 2 × 2 sections by the track grid's own ops (`FUN_0053aac0`: the type,
`0x80`, `0x82`, `0x83`), and the mouth, two cells past the station's far end, handed back. The station's first section is left in
`DAT_007639e0`/`e4` by `TrackRide_LayStation` (`0x0052a31c`), but the place commit overwrites that at once
(`0x00524fb6`..`0x00524ff3`; an add-on's put-down the same, `0x00524cb4`..`0x00524d01`): it ends as the mouth
(`FUN_0052a3a0`: x + 4, z + 1 at turn 0) and `e8`/`ec` as the other mouth (`FUN_0052a420`: x − 2, z + 1), and the
circuit's walk (`TrackRide_WalkCircuit`) starts and stops 4 cells back from that, along the turn (x − 4 at turn 0,
`0x0052a9c2`), on the station's first section. `FUN_0052f580( 0xb )` (or `0x10`) then arms the track tool, whose type the next
ops lay at the mouth (`0x87`, the type, `0x80`, `0x83`, `0x85`, `0x86`, `0x81`, `0x00524e65`..`0x00524f9a`) and the
same at the other mouth (`0x0052506c`..`0x0052518c`), and **the queue's seeding re-arms mode 3 over
it, so the queue comes first**. Measured at turn 0 on preview cell (32,25): the footprint x 30 to 33, z 24 to 26,
entrance (31,24), exit (32,24), the queue's cell (31,23) and a path cell before the exit (32,23); on the track grid the
station (0xd) at (30,25) and (32,25), laid track (0xb) at the mouth (34,25) and at (28,25), where the circuit comes back
in, every other cell of a 2 × 2 0xc; mIsTrackRideValid (`+0x2c`) 0; the tool 4 then 3 (`q581/orig/A-8-put.png`). Splish
Splash is the same a row further up its 4 × 4 (0x12, 0x10; its other cells 0xc too: op 0x83 stamps 0xc in both kits,
`0x00538895`, and no writer of 0x11 was found, though readers test it; not measured).

**The track tool** (modes 0xb and 0x10, arm `0x00526cb3`; the dispatch at `0x00525a1c` also sends mode 0x19, track type 0x19's tool, to its
own arm `0x00525a52`). Read for Q762 in Ghidra (`q762/dump`; `Track_CellOp` is `FUN_005370e0`, named then).
**Armed** by a click on a piece of track, "Left-click to edit this track": the hover sends track types 0xb and 0x10, and
0xc under any parent but a hoarding, to category 5 (`0x00487392`), so a water piece's 0x11 cells and the stations arm
nothing; the click (`FUN_004879d0`: the cell's type is looked at from `0x00487f39` to `0x00487f69`, a 0xc cell's anchor taken at
`0x00487f39`..`0x00487f4e`) selects the anchor's owning thing and
installs mode 0xe (karts) or 0x13 (water). That mode's release (`0x00526c19`..`0x00526c4d`), when the ride's `+0x2c` is nought, walks
the ride's track from the mouth (`FUN_00530830`): in 2-cell steps along laid track (0xb, 0x10), turning at a bend the
way its links leave it (`mask ^ opposite( heading )`), until a cell with no link onward or the station met from its far
side; the pending list `0x0081b740` is the mouth, each bend and the cell it stops on, which is the anchor; each cell's
overlap counter `+0x20` is set, 1 at a bend or crossing, 0 on a straight or an end (`FUN_005228a0`). Then
`FUN_0052f580( 0xb )` (or `0x10`), which keeps the anchor. With `+0x2c` set the same mode takes two clicks and joins the
loop at them (`FUN_00539760( 0xe )`, `FUN_0052b400`), not read further. **A click** on release: a run the preview
flagged red (`DAT_00816d48`) lays nothing, plays 0xaf and puts the tool away (`0x00524a63`..`0x00524acd`); otherwise
sound 0xce, the target moved onto the anchor's row or column, whichever is farther, a tie to the row
(the snap, `0x00526d4f`..`0x00526d85`), and the run laid by ops `0x87`, the type, `0x80`, `0x82`, `0x83`, `0x85`, `0x86` and
`0x81`, each a pass of `FUN_0053aac0` over the whole line (`0x00526daa`..`0x00526ebe`; the close that follows is
`0x00526eff`..`0x00526f83`, its `0x80` stores `0x00526fa7`..`0x00526fb2`). That walker takes the target onto the anchor's 2-cell grid
(an odd distance to the lower cell) and writes it back, steps ±2, and gives each cell the step (`0x00820ac8` marks the
first cell, `0x00820ad0` the last); an op that answers nought stops its own pass. What each op does to a kart or water
cell (`Track_CellOp`):

| Op | On the run's anchors |
|---|---|
| `0x87` | a cell that is not track: its 2 x 2's things torn down (`FUN_00527ee0`) and, the last, its map cells cleared, both gated on `[0x00818c14] < [0x00818c10]` (`0x00538cf0`..`0x00538cfd`) |
| the type | `Track_StampCell`: its own type again is free and bumps `+0x20`; another track type refuses; a new piece is charged `Costs.KartTrackCell` or `WaterTrackCell` (not in modes 4 and 0x3b) and refused above the balance; `0x00820ae8` notes a last cell that was track already |
| `0x80` | `FUN_00539220`: links the cell and the anchor two back along the step, where that one is track (0xb, 0xd, 0x10, 0x12, 0x19) of this ride or none and neither has four links; the run's first cell links along the last pass's step (`0x00763b00`/`b04`; none after `0x80`, which putting the tools away, Backspace's mode 0x35 and a closing run write) |
| `0x82` | `FUN_00539670`: the way on (`+0xd`) is the step's bit on a cell of under three links, or gains it on a crossing |
| `0x83` | with the station pointer `[0x008186c4]` set: the anchor's owner `+0x10` the selected thing's cell, and its three other cells stamped 0xc, in both kits (`0x00538895`), naming it |
| `0x85` (from `0x00538a92`) | an end or straight over map types 1 or 3 takes flag 1, a hill; over four bare cells its flags' low four bits go |
| `0x86` | for track with an owner, outside a fresh boundary: `TrackRide_ShapeFromMouth` with 1, then `TrackRide_ShapeFromOtherMouth` with 0 when it answers 0 (`0x00538ca0`, `0x00538cb7`) |
| `0x81` | a bend, or a crossing with links, loses its hill flags; every piece but a station is retiled; on the last cell where `0x00820ae8` is set it answers nought |

While `0x81` answers not nought and the target is not the anchor, the anchor moves to it and the tool stays, the pair
`TrackRide_ShapeFromMouth` with 0 and `TrackRide_ShapeFromOtherMouth` with 1 following (`0x0052718c`): "A track piece's
model, price and open end", below. When it answers nought (the run met track) or the click
does not move the anchor, the circuit is walked into `+0x2c`; with 1 and the open guard (`FUN_004df290`) the ride opens
(`FUN_004df390`); the pieces are put to clip 0xc (`TrackRide_PlayPiecesFromMouth`, `FUN_0052a490`, and with no circuit
`TrackRide_PlayPiecesFromEnd`, `FUN_0052a700`, too; `BUMP 17` plays them 5 or 0xc the same way); the anchor goes to −1,
and the tool to the queue (mode 3 at the entrance's front) when the queue pointer `0x008186c0` is set, else idle. An
add-on's put-down (`0x00524c00`) clears the ride's sections (`TrackRide_ClearSections`, `FUN_00545450`) and walks again;
a move's (mode `0x3b`) walks nothing.
Measured: clicks on (34,25), (34,29), (28,29), (28,25): 0xb with the anchor at each, `+0x2c` 0, then 1 and idle on the
last.
**The preview's verdict** (`Track_CellVerdict`, `FUN_00539760`, per anchor of the run): any of the four cells' map flag
`0x40` (outside the park's land, `FUN_00535db0`) refuses; map types 4, 7, 9, 10, 30 or 0x15 mark the cell blocked; a
track cell of another ride among the three others latches the refusal; the run's new pieces add up (`FUN_0052fba0`) and
a total above the balance refuses (`DAT_00816d5c`); an empty cell not blocked passes; the anchor's own track passes by
rules on its links and way on (a straight crossed, an end met, the station met along its flow), not read to the end;
where an end of the run meets its own track so, it latches `DAT_00816d4c` (`0x0053a1c6`). The run's last cell, unless
its track is the run's own type, is refused on map type 3 or 9 (queue, or a ride's used-from cell, `FUN_00536320`) or
map flag `0x20` among its four cells (`0x00539fc5`..`0x0053a05f`). No map-type test asks for the river (type 2): a piece
over it is plain, for op `0x85` makes a hill only over types 1 and 3 (`0x00538b0b`), and only on an end or a straight
(a bend, or a cell of three links or more, returns first, `0x00538aa9`..`0x00538b05`). **The pointer** in modes 0xb and
0x10 (`FUN_0052f950`, the byte table `0x0052fa48` sending both to `0x0052f9a9`) is `c_link` (5) under `DAT_00816d60`,
which only the path verdict writes, else `c_end` (`0x12`) under `DAT_00816d4c`, else `c_track.ani` (6); then
`c_noplace` (8) where the preview is red and `c_cash` (`0x14`) under `DAT_00816d5c`, each over the last.
**The preview** (read for Q768, `q768/dump`): `BuildTool_PreviewPass` (`0x005234d0`), every UI tick in modes 0xb, 0x10
and 0x19, takes the pointer onto the anchor's row or column as the click does and calls `Track_RunPass` (`FUN_0053aac0`)
with `op | 0x100` (op `0x10b` karts, `0x110` water) from the anchor (`[0x0081ede4]`, `[0x0081ede8]`; unanchored, from the pointer's cell to
itself), which puts the target on the 2-cell grid, sets `DAT_00820ac8` for the first anchor and `DAT_00820ad0` for the
last, and runs `Track_CellOp` (`0x005370e0`) on each anchor on the map (`FUN_004d8300`). Its square arm asks
`Track_CellVerdict( op, 0, the step's bit )` (`0x005372ad`). An answer of 1 is a red square: the verdict is asked again
with 1 (`0x00537a29`), which past the land copies a pending refusal (`DAT_00816d58`) into the latch (`DAT_00816d50`, `0x00539dbc`..`0x00539dca`, before the third argument is read at `0x00539dd0`), charges an empty cell a second time (`0x00539f61`) and sets `DAT_00816d5c` if
the total then passes the balance, then `FUN_00527940( 1 )` (`DAT_00816d48`, the pointer's red). Any
other answer is the square's texture, 0 blue, or 0xb `m_end`, the verdict's answer where the run's last anchor (or its
first, the pointer having rested on it, `DAT_00818688`) meets its own track by its links and way on, latching
`DAT_00816d4c` (`0x0053a1c6`). The square is queued with `+0x18` 1 (`0x00537a5c`), which `FUN_0053df30` lays 20 wide
(`0x0053df39`; 10 where it is nought, as the path and queue tools' are): one square, turn 1, over the anchor's two by
two, lifted and walled as "A track square's lift and walls" (below) says. Within an anchor the verdict answers in this order: land
outside the park (flag 0x40) red, nothing latched or charged; then, in these track modes, 0 (blue, uncharged, latching
nothing, even where a latch stands) for a map type 4, 9 or 10 cell, of the anchor, its east cell or its south-east cell
(the south cell is never asked), whose owner thing's item `+0x54` (`Info.OverwritePriority`) is below `[0x00818c10]`, which
is 3 in modes 0xb, 0x10 and 0x19 (`0x00539961`..`0x00539c24`; set at `0x00523546`, stored at `0x005244be`; the
category files give 5, a feature 0 or 1), and the click's op `0x87` tears that thing down (`0x00538cf0`..`0x00538cfd`);
another ride's track among the three other cells latches;
a latch answers red before the charge; the empty cell is charged, and a total above the balance latches; then any other
refusal answers red and leaves `g_BuildRefusalPending`, which the square's second judgement copies into the latch at
once (`0x00539dbc`..`0x00539dca`). The
anchor itself, its own end, with the run leading back against its way on, answers red alone (`0x0053a216` to
`0x0053a251`, then `0x0053a4e9`: its links are not three). Measured in the original under Proton (`q768/orig`,
`squares.py` reading `+0x18`, the cursor `[0x007cb2d8]`) on `q581/karts.TPWS`, a Dino Karts bought on (32,25) and the
tool armed on its mouth: the pointer on the mouth, one square (34,25), `m_end`, 20 wide, at 11.5 with three walls, cursor
0x12 and `DAT_00816d4c` 1; on (34,29), (34,25), (34,27) and (34,29) blue and the frame's "$ 400", cursor 6; on (30,25),
the mouth and the station's two sections red, "$ 0", cursor 8; after runs to (34,29) and (28,29), on the other mouth
(28,25), blue, blue and `m_end`, cursor 0x12. The squares over the mouth, the station (map type 4) and the run's end
stood at 11.5 with walls, the other mouth's at 1.5.
**A track square's lift and walls** (Q775; `ParkBuildMarkers.TrackLiftOver`, `TrackWalls`, `ParkItemHeights.AtCell`).
`Track_CellOp`'s square arm (`0x0053716f`..`0x00537251`) starts the lift at nought, or, where the anchor's TRACK record type
is 0xb, 0x10, 0x19, 7, 4, 9 or 10, at the anchor's height rounded up to ten; it passes that to `TrackSquare_Height`
(`FUN_00536ca0`) with the anchor's MAP record (`FUN_004d0b10`, the 0x44-byte grid at `[0x008023a0]`) and the anchor's x and y.
That asks the map type of the anchor's own cell, then of the cells one step south (`DAT_007cdba8`, (0,+1)), east
(`DAT_007cdbc8`, (+1,0)) and the far corner (`DAT_007cdbb8`, (+1,+1), written 1 and 1 at `0x004d9b50`), each through
`FUN_004d96f0`, none off the map; each of the same seven types replaces the lift with that cell's height rounded up to ten, so
the last such cell gives it. **Every type is rounded alike**: the three arms (0xb, 0x10 and 0x19; 7; 4, 9 and 10) are the same
code, `__ftol( FUN_00452ae0( x, y, 0 ) )`, then `(t + 9) / 10` by the multiply `0x66666667` and `* 10.0` (`0x00700d94`). The
square's record is laid at that + 1.5 (`- [0x00700d9c]`, -1.5). The height `FUN_00452ae0` answers is the cell's track-layer
object's first (`FUN_0053bf30`: the track record's `+4`, or for a type 0xc or 0x11 cell its `+0x10` parent's), else the thing
on a cell of map type 4, 9 or 10 (`FUN_00527e80`), each its model record's `.hmp` (`+0xcc`) over the cell plus its root's height,
else the ground at the cell's corner; an object with no `.hmp` answers the ground. A kart or water piece has its own: the kits'
loader passes flags `0x10422`, whose bit `0x400` reads `<model>.hmp` (`0x00462dc9`, the name `.hmp` at `0x0074d360`), and every row
of both kits ships one, two by two. A station's track records (0xd, 0x12) hold no object, so its sections answer the ride's own
`.hmp` through their map type 4. **The walls** (four arms, north `0x0053731e`, east `0x005374d9`, south `0x00537697`, west
`0x00537855`, and the refused arm's four from `0x00537a83`): the anchor two cells away on the track grid (`FUN_0050c320` with 2),
none off the map, lifted by the same rule; where its lift is not equal (`FCOMP`, `0x0053746c`) and its whole part is under the
square's, a record at the neighbour's lift + 1.5, then ten higher while under the square's lift, each face byte 1, 4, 0x10 or
0x40 (`FUN_0053c8d0`'s fourth argument), `+0x18` 1, so `FUN_0053df30` lays it 20 long on that edge of the two by two (its size,
`0x0053df39`, added to the corner it lays first, `0x0053e14b`). A wall's texture is 0 under a square of the blue arm, `m_end`'s
too, and 1 under a red one (`0x00537bf9`); its `+0x98` is 1 in the blue arm only where the verdict answered 0xd (`0x00537307`),
which matters only in the gliding modes 4, 0xe and 0x13. The jump table `0x00536c10` is `FUN_005367a0`'s, the op `0x87` reset's (below), not this rule's.
Measured in the original under Proton this session (`q775/orig`, `run.sh`, tag R2; `squares.py` and `liftcells.py`) on
`q581/karts.TPWS`, the karts bought on (32,25) and the tool armed on the mouth (34,25): the mouth's square at 11.5 with walls
north, east and south at 1.5 (the station's (32,25) lifts 10 too, so no west wall); with the pointer on (34,29) (34,25) the
same, (34,27) and (34,29) at 1.5; on (30,25) three red at 11.5, walls (34,25) north, east and south, (32,25) south, (30,25)
south and west (the anchors (30,23) and (32,23) read row 24, the ride's footprint, type 4, and its exit, 0xa); after a run to (34,29), on (40,29)
over the Security Camera: (34,29) at 11.5 walled east, south and west, (36,29) and (38,29) at 1.5, (40,29) at 31.5 walled north,
south and west at 1.5, 11.5 and 21.5 and east at 21.5; after a run to (28,29), on the other mouth (28,25): (28,29) at 11.5
walled north, south and west, (28,27) at 1.5, (28,25) `m_end` at 1.5. `liftcells.py`: the mouths are map type 0 with track
0xb, their pieces' objects (93, 94) hold a `.hmp` and stand at -2 and -7, so the mouth reads -2 + 14 / 2.55 = 3.49 (10) and the
other -7 + 5.49 (nought); the station's sections are map type 4 with no track object. **Ours** reads the same rule over each
cell's `AtCell` and draws the same records in every one of those five poses (`q775/ours-a`). **The path and queue tools'
squares ask the same height** (Q776; `ParkBuildMarkers.LiftOver`, `ParkItemHeights.PieceTopAt`): `FUN_00532fc0`'s lift is
`ceil10( trunc( FUN_00452ae0 ) )` over a map type 4, 7, 9, 10 or `0x1e` cell or a track kind 11, 12, 16, 17 or 25 cell, and
`FUN_00452ae0` reads the cell's track-layer object first, so a one-cell square over a kart or water piece stands on the
piece's `.hmp` and its root. **A straight or end piece does not refuse a path square; a bend, a junction or a 0x19 cell does** (the verdict `FUN_00535670`
answers red with `d58` 1 for a corner or junction record, `0x00535bd7`..`0x00535bef` -> `0x00535d57`, and for track type 0x19
with latch `d50`, `0x005358b5`, each after the 0xc/0x11 redirect; the mouth's links are one, `0x40`, so it is an end and passes):
a path tool anchored on bare ground at (34,23) with the pointer over the mouth (34,25) draws blue (texture 0) squares, the last
lifted (the preview; no click was made). Measured in the original
under Proton (`q776/orig`, `run.sh`, tags O and P3run; `squares.py`, `liftcells.py`) on `q581/karts.TPWS`, the karts bought
on (32,25): (34,23) and (34,24) at 1.5; the mouth (34,25), track 0xb, object 93 with a `.hmp`, root -2, at 11.5 with one wall,
face 0x1, at 1.5; with the pointer on (34,26), a 0xc cell parented to (34,25), (34,25) and (34,26) at 11.5, walls north of
(34,25) and south of (34,26), both at 1.5 (the east and west neighbours lift alike); the other mouth (28,25), root -7, with
the anchor on (28,22): four squares at 1.5, no wall. Ours draws the same records in the three poses (`q776/ours-a`, `strip`).
**The boundary's things lift a square too** (Q777; `ParkItemHeights.PieceTopAt`, `ParkBoundaryHoarding.PieceAt`). A boundary
anchor is track type 0x19 and its other three cells 0xc, parented to it, so `FUN_0053bf30` answers its model for each; the loader
passes flags `0x422` (`0x00522eb1`), whose bit `0x400` reads `<model>.hmp`, and `hoarding.wad` ships one for each of its nine models
(156 bytes, two by two; the maxima over the cells: `ho_fos1`, `ho_end1` and `ho_fos4` 51 all four; `ho_fob1` and `ho_fob3`,
`ho_fob4` 0 beside three 51s; `ho_fob2` 25 beside three 51s; `ho_fos2` 76, 76, 71, 71; `ho_fos3` 56; each byte / 2.55, so 51 is
19.99997, truncated 19, rounded up to ten 20). The anchor's record names its model: the row at `+0x18` and the rotation at `+0x1c`
(family 5 at `+0x14`; the file's `mTileData`), the piece standing on the anchor moved by the turn at 360 less it
(`ParkBoundaryHoarding.PieceOf`), so the cell's height is the file's byte over the cell plus the root's, the middle of the lowest
and highest ground under its two by two. Over the stock park's east wall (ground 0 from x = 91) the anchors of row 2 (`ho_fos1`) lift 20 over
their two by two - (93,21), turned 180, and (94,21), its 0xc cell, which Q436 measured - those of rows 22 and 25 (`ho_fos2`,
`ho_fos3`: (93,9), (93,13), (93,17), (93,19), (93,23)) lift 30, and the corner pieces of row 3 (`ho_fob1`, (93,1) and (93,31)) 20
beside the one cell whose byte is 0. The wall has no row 19. **A path or queue tool's square over a boundary cell is refused** (texture 1) in the original, and so
is every square after it, as over any track corner; the track tool's, anchored on a cell of track type 0x19, is lifted by
`Track_CellOp`'s start (`0x0053716f`) and answers red too. Measured in the original under Proton this session (`q777/orig`,
`run.sh`, tag O5; `squares.py`, `liftcells.py`; `liftcells.py` reads (93,21) track 0x19, object 42, `.hmp` yes, root 0.00; (94,21) and
(93,22) track 0xc via (93,21); (93,25) 0x19, object 44, its 0xc cells via it) on `q581/karts.TPWS` (the stock park): the path tool
anchored on (91,21) (1.50, texture 0), pointer (93,21): (91,21) and (92,21) at 1.50 texture 0, (93,21) at 21.50 texture 1 and
two walls west of it at 1.50 and 11.50 (face `0x40`); pointer (94,21): (93,21) and (94,21) at 21.50 texture 1, walls west of
(93,21) and east of (94,21), each at 1.50 and 11.50; pointer (92,21): the two squares at 1.50, no wall. The track tool, Dino Karts
bought on (85,25), armed on its mouth (87,25), pointer (93,25): (87,25) at 11.50 with north, east and south walls at 1.50,
(89,25) and (91,25) at 1.50, (93,25) at 21.50 texture 1 with walls east and west at 1.50 and 11.50. **Ours** draws the same
records in all four poses (`q777/ours-a`): `91,21,0^0;92,21,0^0;93,21,1^20` with the two west walls, and so on, the last at
`93,25,1^20#2`. A fresh park's anchors carry the same three fields (`FreshParkBoundary`, from the chooser's row and turn), where
before the walk's pieces were only in the boundary's own list. A boundary anchor whose record names no row of the table is
counted (`MARKER_LIFT_OVER_TRACK_THING`) and stands on the ground; no park to hand holds one.
**A run over scenery** (Q773; `ParkTrackTool.JudgeCell`, `TearDownUnder`; the click's pass in `LayRun`). The verdict's blue answer is at
`0x00539961`..`0x00539c24`: after the four cells' flag `0x40` (`0x00539924`..`0x0053995b`), for each of the anchor (the
verdict's own cell), its east cell and its south-east cell in turn, the last read twice by the listing (the south cell, `ebx`,
is never asked), a cell of map type 4, 9 or 10 whose owner is a catalogue object (the thing's model byte `+2` is 3) sets
`[0x00818c14]` to the item's `+0x54` and answers 0 where `[0x00818c10]` (3) is above it, returning at once, before the latch,
the other cells' refusals and the charge. Where the owner is no catalogue object `[0x00818c14]` keeps what it held; the
preview pass clears it to 0 at its start (`0x00523522`). Op `0x87` (`0x00538cf0`..`0x00538e37`) runs the same compare, then, for
an anchor whose track type (the track grid cell's `+8`) is not 0xb, 0xd, 0x10, 0x12 or 0x19 (`0x00538d45`..`0x00538d6c`), calls the demolisher on
(x,y), (x+1,y), (x,y+1) and (x+1,y+1) in that order (`0x00538d76`..`0x00538d9e`); `FUN_00527ee0` resolves the thing by
`FUN_00527d60`, which answers for a cell typed 4, 9 or 10, so a cell already cleared by an earlier call finds nothing; on the
last anchor (`[0x00820ad0]`) it then calls `FUN_005367a0( 0, 0 )` on the anchor's four cells (`0x00538db3`..`0x00538e37`),
whose jump table (`0x00536c10`, bytes `0x00536c2c`) acts only on map types 1, 3, 4, 9, 10 and 0x15, so bare ground, the river
and track are left as they are. The gate reads what was last stored: the priority of the last asked
map type 4/9/10 cell with a kind-3 owner, or the target cell's owner where that is map type 4 (the preview's tail,
`0x0052462f`..`0x005246fb`, which runs after the walk; the click runs op `0x87` at once, `0x00526daa`..`0x00526dbb`, with no verdict
in between). A run onto a cell the verdict reads at priority 3 or more and still lets through (the station along its flow: its
map cells are type 4 of the ride, 5; `0x0053a251`..`0x0053a27e`, `0x0053a2c7`..`0x0053a2f4`) finds the compare at `0x00538cfd`
shut, and nothing on that run is torn down or cleared. Every run this game lets through reads below 3, since it refuses the run
into the station (`TRACK_RUN_INTO_STATION`), so the compare stands open at the click; **the south cell is torn down with the rest**, though the verdict never asked
it, so a ride's footprint on a south cell goes with the feature on the east one (decoded, not seen). The click charges the cell's
piece at the stamp as it does any new piece (`Track_StampCell`), where the preview's total leaves the blue anchor out.
Measured (Q773): in the original under Proton (`q773/orig`, `run.sh`) on `q581/karts.TPWS`, the Dino Karts bought on (32,25), the
track tool armed on the mouth and a run clicked to (34,29), the pointer on (40,29), where the Security Camera (`camera`, priority
0, one cell, thing 19) stands: four blue squares (34,29), (36,29), (38,29), (40,29), the last 20 wide at y 31.5 with three
wall levels down (the lift rule's map type 4 cell, built by Q775), cursor 6, the frame's "$ 400" (two
pieces: the cell over the camera is not in the total, `O-O2.png`); the click laid pieces on (36,29), (38,29) and (40,29) (the
last `gk_trckQ`, row 1, turn 90), the camera's pole gone from the frame (`O-O3.png`) and the anchor moved to (40,29), the tool
armed still. The bank fell 445 over that click, the park's clock running (the day turned 2.19 to 2.20 between the two frames):
600 for three pieces less a refund of about 155 and the clock's own costs, not separated; ours refunds the camera's 250
whole (the scrap percentage, `SCRAP_VALUE_DEPRECIATION`, is not built) and falls 350 on the same clicks, the clock held.
**Ours** answers the blue and tears the thing down as above; a feature of two by two or more reaches the south cell, which counts as
part of the answer where one of the three asked cells is the feature too. Where another cell of the anchor is refused by a rule
of ours (a ride's footprint on the south cell, path, queue, another ride's track, map type 7 or 0x15) the run stays red and is
counted, `TRACK_RUN_OVER_SCENERY_MIXED` (Q774): the original lays its piece over that cell too.
**OpenTPW** (Q762, `ParkTrackTool`) builds the arming walk, the click and its passes but `0x87` and `0x86`; its verdict
refuses the last cell's queue, used-from cell and flag `0x20` as the original, lays over the river plain, and refuses and
counts path and queue mid-run (`TRACK_RUN_OVER_PATH`: the original's hill, unbuilt), its own track mid-run or with two
links at the end (`TRACK_RUN_CROSSING`) and its own station (`TRACK_RUN_INTO_STATION`); over a feature of priority below 3 it
answers blue as the original does (Q773, below); a closed loop's edit
(`TRACK_EDIT_CLOSED_LOOP`) is counted, the circuit's walk that closes a run is Q763's (below), and Backspace's refund
Q765's ("A track piece's model, price and open end", below). The preview (Q768, `ParkTrackTool.Preview`,
`JudgeCell`) draws a two-by-two square an anchor, blue, red, or `m_end` where the run's last anchor is its own track,
judged in the original's order over ours' own refusals (the counted ones too), the total running on past a refused cell
with the red squares' second charges (`0x00539f5c`, `0x00539f87`..`0x00539fb6`). The pointer (Q765, Q768,
`ParkTrackTool.CursorOver`) is taken from it: `c_end` or `c_track`, `c_noplace` over either where a square is red, and
`c_cash` over that where the total passed the balance, so a refused run can show `c_cash`; each square is lifted and walled
as the original's (Q775, above). Not built: the run's total on the screen (Q541), and the
anchor judged a last for the one pass after the pointer leaves it. Measured in ours (`q762/ours-b`, then `q763/ours-d` with the walk): Q687's two loops give
the anchors the links, way on, overlap, row and turn the original's `A-load.log` and `W-cells.log` hold, the closing
corners included, and 200 (250) a new piece, **but not by the original's own clicks**: the original's water loop took
four clicks, (22,17) first (`W-cells.log` 79.53 s, then (22,15) at 103.53 s), where ours took three, (22,15), (16,15),
(16,19), so (22,17) reads overlap 0 in ours and 1 in the log and the charges are 500, 750 and 250 against its 250, 250,
750 and 250; and the karts' loop skipped the original's Backspace and re-lay (199.64 s, 209.89 s), the end state the
same. The original's own sequence is not repeated for the water loop.

**The walk** (`TrackRide_WalkCircuit( check )`, `FUN_0052a970`) goes from the circuit's start in 2-cell steps the way
each cell's flow (`+0xd`) and links (`+0xc`) send it: a cell with two links at right angles is a bend
(`TrackCell_BendLinks`, `FUN_0053ae00`), two opposite a straight (`TrackCell_StraightLinks`, `0x0053ae50`), three or
more a crossing (`TrackCell_CrossingLinks`, `0x0053ae90`), through which it goes straight on. It answers 1 when it is
back at its start, and 0 at a cell that is not track (`TrackCell_IsTrack`, `0x0053ad30`: 0xb, 0xd, 0x10, 0x12, 0x19) or
whose way on has no link. Unless checking, it first lets the ride's sections go and makes one a piece
(`TrackRide_AddSection`): bend 5 (links north and east), 6 (north, west), 7 (south, west), 8 (east, south); straight 9
east–west, 10 north–south, 12 and 13 the same with an add-on (the cell's flag 0x10, "Found upgrade... modify?"; the
section keeps the add-on's model); crossing 11. The first two, the station's, are flagged 0x10000 and lay no buoy. A
cell's `+0x24` takes its step (`TrackCell_SetStep`, `0x0053aef0`). Read for Q763 (`q763/dump`,
`q762/dump/0052a970.asm`): the start is `[0x007639e0]`/`e4`, the mouth `FUN_005276d0` or the put-down leaves there, 4
cells back along the turn, so the station's first section, and the heading `[0x00818c50]`, which `FUN_005276d0` sets to the
turn's way (east, north, west, south for 0, 90, 180, 270: `FUN_0052a3a0`) at every click that is not red (`0x00524af6`,
`0x005278c7`; the put-down the same at `0x00524cf1`). A click that does not move the anchor and is not on the mouth runs
no pass and walks with it (`0x00526d86`..`0x00526da4`); otherwise op `0x86`'s `TrackRide_ShapeFromMouth` writes the turn's
way again (`0x0052c791`) and, where it answers 0, `ShapeFromOtherMouth` its opposite (`0x0052cd48`; `FUN_00530830`'s second
write the same, `0x00530bdd`), so the walk heads out of the mouth except at a click on the mouth while the anchor is the
mouth, which walks back towards the other mouth (the original lays 1 section there, ours 2: `+0x2c` 0 in both, and
nothing a player sees reads the difference, `ParkTrackRideTable.LayoutOf` of a run with no bend being the same); a start on a piece's other cell (0xc, 0x11, `TrackCell_IsPieceCell`) asks its anchor whether
there is track and whose ride it is (`0x0052aa1c`), and with none the walk answers 0 before it clears anything. A
straight or crossing keeps the heading; a bend leaves by `mask ^ opposite( heading )`, or `mask ^ heading` where the
bend has no link back (`0x0052b1cc`), checking or not, and **where its way on (`+0xd`) is not that, takes it and is
retiled** (`0x0052b1e3`..`0x0052b1fc`): so a closing run's last bend, left at the run's flow, turns to the way the
circuit leaves it. A cell that is not track, or after its turn has no heading or under two links, answers 0; a cell of
three links answers 0 where the cell two on is of no type, or is track with no links while heading north (`0x0052b22f`..
`0x0052b280`). A laid piece with no model (`+4` nought) is retiled; an add-on's cell shows or hides its model's two end
caps by the track on either side (below). `TrackRide_ClearSections` (`FUN_00545450`) removes the
ride's cars (`FUN_0054ae50`), closes it (`BUMP 6`), frees the buoys not flagged 2 or 4 and every section with its
objects. Callers: the closing click (`0x00526f02`, 0), an add-on's put-down (`0x00524d0a`, 0) and the closed loop's
two-click edit (`0x0052693e` with 1, `0x00526bd0` with 0); each stores the answer in `+0x2c`, and the click and the edit
then open the ride where it is 1 and the open guard lets it (`0x00526f18`..`0x00526f2d`).
**OpenTPW** (Q763, `ParkTrackTool.WalkCircuit`) builds the walk at the closing click: the sections, the bends' way on
and retile, `+0x2c` and the opening; it clears the last walk's sections, their objects and buoys (Q766's, below), and
no car, a kart or water ride having no bumper record (Q588), and the step count is answered, not kept on the cells. The pieces' clips that follow are Q781's ("The pieces' clips", below).

**An add-on's end caps in the walk** (read for Q778 from `q762/dump/0052a970.c` and `q778/dump`; measured in the original
under Proton, `q778/orig/O1`, `O2`). An add-on (Lost Kingdom's Dino Karts Jump 1500 and Tunnel 1501 on the karts, the Splish
Splash Tunnel 1502 on the water) is a placed thing of its own, whose model stands over the cells it covers; the placer's
shape kinds `0xb` and `0x10` set the track cells' add-on flag 0x10 (`FUN_0053a510( 0x10 )`, `0x0052950d`), the cells' map
type is 4 and their parent the thing's cell, and the kit makes no piece there. The walk keeps a count (`[esp + 0x20]`, `0x0052ae18`) and
bits (`[esp + 0x2c]`). The count goes back to nought at a straight without the flag, checking or not, and, only where not
checking, at a bend or a crossing (their arms are skipped when checking, `0x0052ab18`). At every track cell
(`0x0052ade3`, `Cell_HasFlag( 0x10 )` on the track record's `+0xe`) with the flag, the count goes up by one and the thing is
looked for: `FUN_00527d60` takes the cell's map record where its type is 4, 9 or 10, its parent, and the occupant of that
parent; with none, nothing more. With one, the cell two on along the cell's way on (`+0xd`, `FUN_0050c3e0( way, 2 )`) ORs 0x10
into the bits, and the cell two back along the opposite way 1, each where it is a kart or water anchor with a model (`+4`, so
not another add-on's cell) or a station (0xd, 0x12). The bits are never cleared after the walk's start (`0x0052ae1e`
runs only at a count of nought, which follows a raise). At a count of exactly 2 (`0x0052af54`) the bits choose the caps:
`FUN_0044b3c0( instance, 1, side )` finds the first lookup record whose hide word (`+8`) is `side` and whose flags meet 1, so
the cap of hide word 1 is hidden (`| 0x80000010`) where bit 1 is set and shown (`& ~0x10`) where not, and the cap of 0x10
the same by bit 0x10 (0 both shown, 0x11 both hidden). So an add-on of one piece (the Jump, one anchor) is never reached and
keeps its caps as its model was made, no mask passed (both shown); a Tunnel of two pieces with track either side hides both;
an add-on beside another counts on through it, and a later add-on takes an earlier one's bits. The add-on's own put-down
walks the circuit (`0x00524d0a`; `park-engine.md`, "The add-on's snap"), so a bought one's caps are set at the click where
the walk reaches it (an open loop may not). A park file keeps the caps in
its node words (bit `0x1`, FileFormats `saves.md`, "The node flag words"), which the load lays back. Measured on Alexah's
played Full Simulation Dino Karts (a Full Simulation player, `original/fsload.sh`): the Tunnel's `gk_end09` and `gk_end08`
read `0xc0000111` (hidden; the file's words `0x401`), the Jump's `gk_end02` and `gk_end01` `0x40000101` (shown; the file's 0).
Measured at a put-down (Q780, `q780/orig/O1`): a Tunnel bought onto the straights (12,15), (12,17), bends with models either
side, read `0xc1000111` at once (hidden); a Jump bought onto (16,13), between the crossing and a bend, `0x41000101` (shown).
**OpenTPW** (Q778): the walk keeps the same count and bits (`ParkTrackTool.WalkCircuit`) and shows and hides the caps
(`ParkObjects.ShowAddOnCaps`, `ModelFile.FindNodeByHide`); a loaded add-on lays its two caps' saved words
(`ParkObjects.SavedCaps`), the rest of a model's words left to this game's clips; the walk finds the thing as the head of the
parent's live list (`ParkState.CellAt`), so a thing just put down is found. Seen in this game (Q780, `q780/R1`): the put-down's
walk hides the bought Tunnel's caps and leaves the bought Jump's shown, as the original's. The played file's model records do
not read here until Q810, so the loaded Tunnel's caps were not laid at the load and the first walk hid them. Since Q810 the file's model records read and
the log says "'Dino Karts Tunnel' (thing 404) stands with its end caps as the file left them: 0x1 hidden, 0x10 hidden" and the Jump's "0x1 shown, 0x10 shown"
(`q810-a7/run3fs.out`; counted `TRACK_CAP_LOCK_LOADED` 1x; not photographed). The add-ons themselves are drawn: the
sky `q688/ours-f2` showed under them was that run's Instant Action catalogue, which holds no `upgrades` item (its log:
"nothing in this theme is catalogue number 1501"), and a Full Simulation player's load stands both where the original's do
(`q778/R2`, `O1`, `O2`).
Measured in ours on the final binary (`q763/ours-d`, `ours-t`), by the original's clicks: the karts' ten sections as
`q581/orig/A-track.log`'s, the water loop south's as `D-track.log`'s, `+0x2c` 1 and the ride not opened (theirs the track-ride slot's
state 0, `+0x50`, ours `mCanLoad` 0); (28,25) way 0x04 as `A-load.log`, Q687's water corner (16,19) way 0x04, `wr_trckB2`, row 19, turn 270 as
`W-cells.log` (earlier evidence, the original not run for Q763). The karts opened once their queue joined a path
(`C-track.log`'s state 1 after its queue, another queue: ours joined the cell before the exit).

**A section** (`TrackRide_AddSection( handle, x, z, type, … )`, `FUN_0054b2f0`, pool `DAT_00877b80`, 1024 of 0x28; x and
z its top-left cell × 3072) joins the ride's list (`+0xbc`); a place already there is refused
("Duplicate track section - IGNORED!"). Its collision objects (pool `DAT_00877b70`, 2048 of 0x20: flags, x, z, two
sizes `+0xc` and `+0x10`, next, section, buoy; `Bumper_TakeObject` `0x0054b1e0`, `Bumper_MakeObject` `0x0054b210`), with
`w` the template's `+0x0c` and `r` the car's radius (`+0x08`), each put at the head of the section's list (`+0x1c`): a
straight is one rectangle (0x3f, 0x20 square; 0x13f for an add-on's, 12 or 13) at its middle, 3104 (0xc20) along and `w −
r` across (types 9 and 12 along x, 10 and 13 along z); a bend (1 to 8, 1 and 5 alike and so on) a quarter circle of
radius `3072 + w − r` about the corner it turns round (0x1f less the bits of the two sides it is open to: 2 x below, 4
x above, 8 z above, 0x10 z below; not whole, so a car in it gets three quarters of its thrust, `Bumper_SteerToward`), then
the same quarter of radius `3072 − w + r` flagged 0x40, an obstacle (`Bumper_PushOffObstacles`): 5 about (x + 6144, z)
less 0x14, 6 about (x, z) less 0x12, 7 about (x, z + 6144) less 0xa, 8 about (x + 6144, z + 6144) less 0xc
(`0x0054b42a`..`0x0054b7df`); a crossing a round object at its middle (radius `ftol( sqrt( (w − r)² + 3072² ) )`,
`0x0054b8ff`: 3279 for the karts, 3082 for the water ride; flagged 0x80 for the water family, −5, −8, −12 and −13) and
four obstacle quarters of radius `3072 − w + r` at its corners, each with the corner's bend's bits. Its buoy
(`Bumper_MakeBuoy`, `FUN_0054b170`; pool `DAT_00877b78`, 1024 of 0x1c: flags, its pool index, x, z, ride, next,
object), flagged 1 and tied to the section's first object made (`+0x18`; the object's `+0x1c` back to it), is at a
bend's arc middle, x + a and z + 6144 − b with (a, b) (0xf84, 0xf84) for 5, (0x87c, 0xf84) for 6, (0x87c, 0x87c) for 7
and (0xf84, 0x87c) for 8, or at a straight's or crossing's middle, and joins the end of the ride's buoys (`+0xa8`, count
`+0xac`); a section flagged 0x10000, the station's, lays none (`0x0054bae5`). Every pool answers nought when full ("Out
of Collision Objects", "Out of Buoys"): with no buoy the section is kept, but with no object the next store goes through
nought, and so does a type outside 1 to 13 not flagged 0x10000, whose buoy is tied to no object (`0x0054bb49`). The
ride's free (`FUN_00545610`) lets every buoy go, then the sections and their objects, then the arena's (`+0xc0`).
**Measured**: every section, type, place, object and buoy of both rides' ten as predicted (`A-track.log` 496 s;
`D-track.log` 171 s): the karts' straights 3104 × 1148 and bends 4220 and 1924, the water's 3104 × 257, 3329 and 2815.
**OpenTPW** (Q766, `ParkTrackRideTable.Lay`, `ObjectsFor`, `BuoyFor`) makes every section's objects and buoy for the
karts' and the water ride's templates; another theme's template is counted (`TRACK_SECTION_TEMPLATE_<n>`), and so are
the two the original does not survive (`TRACK_SECTION_OUT_OF_OBJECTS`, its pool holding the track's alone here, and
`TRACK_SECTION_UNKNOWN_TYPE`); the grid's and start's buoys are Q588's. Measured in ours on the final binary
(`q766/ours-b`), by Q581's clicks: both loops' buoys and objects as the two logs' (earlier evidence, the original not
run for Q766).

**The two templates** (`0x764178`, "How a bumper ride ends a go"): −4 duration −3 (laps), car radius 900, `w` 2048,
meshes 4 from 0 (`gk_blue`, `gk_green`, `gk_orange`, `gk_purple`), thrust 16–30, friction 960–950, turn 18–34,
restitution 900, most cars 8, arrival radius 2048; −5 duration −1, radius 1024, `w` 1281, one mesh (`wr_ring`), 4–9,
970–1000, 7–14, 980, most 64, arrival 512. At 60, measured: 24, 954, 27, 900 and 7, 988, 11, 980. `Bumper_LayRide`
(`FUN_00545890`) puts the centre and the unload point (`+0x74`, `+0x90`) at the placer's point and lays, for the karts,
a grid buoy flagged 2 for each car slot `i` below most at `C + R( (most − i − 2) × 1024, i even ? 1024 : −1024 )`, then
the start buoy flagged 4 at `C + R( −3072, 0 )`; for the water ride the start buoy alone, its unload point `C + R(
−3072, 0 ) / 3`. `R` is the turn's flags `+0xb0` (0, 5, 6 and 1 for 0, 90, 180 and 270): 1 swaps the pair, then 2
negates x and 4 z. State 0. Measured: all nine and the one, and the water's unload point.

**Opened** (`BUMP 7`, `Bumper_OpenRide`, `FUN_00544840`), only from state 0: the race's first buoy `+0xa4` is the first
after the start buoy not flagged 2 (wrapping), and the karts go to state 1 (loading), the water ride to 2 at once. `BUMP
14` stores minus the laps; `BUMP 3` puts the karts to state 2 and times every car to them (not the water family's).
Measured: karts 0 → 1 → 2, water 0 → 2.

**A car** (`BUMP 4`, `Bumper_LaunchCar`) takes the whole boarding list and stands at the centre, mesh `cars % count +
base` (the karts' colours in turn, green first). `Bumper_Retarget`'s arm clears flags 2, 4, 8, 0x10, 0x20, 0x40 and
0x40000 (mask `0xfffbff81`, `0x0054a57c`), sets patience 0, and for a new car (0x100000) starts its own looped sound (the sound script's slot 0, for every
car, not the lead's alone: `audio.md`) and, a kart, its emitter (`0x100` id 1) and, flagged 0x2000 (−9 and −10 set
0x3c00), a second model. A kart in a loading ride takes the n-th buoy flagged 2, n its ride's cars, flags | 0x10048
(0x40: to the grid); in a running ride the race's next buoy (`+0xa4`'s when it has none), flags | 0x10008, and a thrust
bonus `+0x68` = `(3 − (rand & 7)) × (thrust / 9)` from the park's generator; a kart retargeted in state 0 or 3 jumps
to `0x0054aa1c`, which clears only 0x100000 and gives it no buoy (no run shows a kart launched then). A water boat
takes the race buoy in any state, bonus 0.

**The buoy arm** (`Bumper_CarTick`). The timer counts down only while at least 0, so a lap count never moves there. A
car at its buoy (flag 8, no 0x20) steers at it; within the arrival radius: a grid car (0x40) within a quarter of it is
flagged 0x90000 (0x80000: no thrust) and takes the race's first buoy; a race car at the start buoy (flag 4) in state 1 or 2
adds 1 to its timer, and at 0 (or in states 0 or 3) ends: 0x20, its offset nought, and a kart in state 2 puts the ride
to state 3 ("GoKart Race Over"), after which every other kart ends at its next start buoy; otherwise it is retargeted.
Then, if the buoy has an object and the car is not in it, it keeps that buoy with its offset zeroed; else it takes the
next buoy in the list not flagged 2 (wrapping to the head), the offset's x to z and x `arrival / 2 − rand % arrival`.
**An ended car** steers at the unload point and within the arrival radius is removed (`FUN_0054ae50`: its riders to the
leaving list; a kart plays sound 1; a ride left with no kart goes back to state 1, "Ride Over - reset to loading"; the
water family's stays at 2). Measured: a kart buoys 9 to 16 and the start 8, its timer −3, −2, −1, 0 on the three passes,
state 3, removed by the unload point, state 1 and the next rider's kart at the centre; a boat 18 to 25 and the start 17,
ended on its one pass, removed, the ride in state 2 throughout (`C-4-race.png`, `D-13-boats.png`).

**The step** (`Bumper_StepCar`). A kart's drawn heading turns to its velocity's as a bumper car's does; under speed 41
it plays clip 0xc, at 41 or more clip 5; worn (`+0x54` 1) and over 80, every 32nd tick the private generator gives
particle 1 (two in eight) or 10 (once). A water boat's heading does not follow its velocity: `+0x58` is a spin that a
rim or obstacle contact moves (`0x00549b26`, `0x00549787`; with p = ±48 × a term not read: p when the old spin
already has p's sign, else old + p), held at × 0.998 a tick while over 48,
and its turn `+0x5c` is `+0x58 / 128` held to ±16; particle 0x60 one tick in eight of every 32nd in a go. Each car's
looped sound is pitched speed / 3. Measured: 1287 to 1266 in eight ticks, turn 13 at 1772.

**Not read**: the add-ons (12 and 13) and the hills (`gk_trckH*`; their rows and their reshaping by the mouths' walks
are below, not measured), the seat a rider takes (`FUN_00549c60`), a car's height over the track, −9's and −10's
flags 0x3c00, and the term in a boat's spin. Which model a piece draws, its price and refund, and the mouths' walks are
read: "A track piece's model, price and open end", below.

**The boundary hoarding's piece** (track type 25; fork watch 2026-10-24, `verify-world-other.md` L3; Q620). The cell
retile `FUN_0053afa0` (`Track_RetileCell`) asks `FUN_0053a540` (`Track_PieceForCell`) for a piece, keeps its family, row and rotation at the track record's `+0x14`,
`+0x18` and `+0x1c` (`0x0053b02b`..`0x0053b04c`) and, for family 5, frees the model the record holds (`FUN_00522a90`) and
makes another (`0x0053b16a`). **The row and rotation:** `FUN_0053a540` looks type 25 up by the cell's neighbour mask (the
track record's byte `+0xc`, `FUN_00522770`) in table `0x00762ff0` (`BoundaryPieceRows`), sixteen rows of `{ row | 0x40000, rotation, mask }`
(count `[0x00700c1c]`), an exact match first, then the first row whose mask is a subset:

    mask  0x11 0x44   0x10 0x40 0x01 0x04   0x14 0x50 0x41 0x05   0x15 0x45 0x51 0x54   0x55   0x00
    row   2    2      1    1    1    1      3    3    3    3      5    6    7    8      4      0
    turn  0    90     0    90   180  270    0    90   180  270    0    0    0    0      0      0

On the gate's two ends (`[0x007639f0]`..`[0x007639fc]`) the mask first takes `FUN_004d8c00`'s bits. A straight (two
neighbours by `FUN_00522810`, opposite: `0x01` and `0x10`, or `0x04` and `0x40`) whose byte from `FUN_00522850` is `0x40` or
`0x10` turns 180 more. The track record's flag word `+0xe` is skipped for a corner (two neighbours at right angles; `0x0053a710`) and
for a cell of more than two neighbours whose mask is not nought (`0x0053a721`..`0x0053a735`), so ends, straights and an
isolated anchor take it: bits 1, 2 and 8 each add 9, 12 and 15 to the row (cumulatively, three separate tests,
`0x0053a73f`..`0x0053a75e`), and bit 4 then makes the row 0x12 (`and ebp,0xffff0000; add ebp,0x12`). A corner (two neighbours at right angles) adds 0x10
where `FUN_00522850`'s byte and the mask are one of eight pairs (`0x01`/`0x41`, `0x10`/`0x14`, `0x04`/`0x05`, `0x40`/`0x50`,
`0x40`/`0x14`, `0x01`/`0x50`, `0x04`/`0x41`, `0x10`/`0x05`) and then takes 0x1b where the flags' low four bits are set; any
other corner with them set takes 0x1a. A straight whose low four flag bits are clear adds a variant, 0 (`[0x00820aec]` under
18), 20 (18 to 23) or 23 (24 to 29), only when it differs from the last piece's (`[0x00820af0]`), and rolls the next from
the park's generator (`FUN_00516330() % 30`, unsigned): a straight whose variant is the last one given stands plain, row 2.
The two words, `[0x00820aec]` (the roll) and `[0x00820af0]` (the last variant), are nought at the start, nothing but this
chooser reads or writes them (`0x0053a938`..`0x0053a983`), so they carry from park to park. The answer is `row | 0x50000`.
`FUN_00522810` (`Cell_CountNeighbours`) counts the mask's four bits and `FUN_00522770` answers the mask, both from the record's `+0xc`, or from
its `+0x22` while `[0x0081b4cc]` is set (the fresh walk, below) and the record's byte `+2` is 2 (not read further);
`FUN_00522850` (`Cell_GetDirection`) answers the direction byte `+0xd`. **Measured** (Q656, `q656/model.py`): this rule gives the row and turn
of every anchor in 42 park files of the original's: Alexah's eight Full Simulation files (the fresh jungle, fantasy and
Hallowe'en parks and their played saves, the jungle ones with land bought, 163 anchors), the reference install's stock
park restart (143) and 33 fresh Lost Kingdom parks made under Proton, a straight's 22 and 25 taken as its 2.
**A fresh park's boundary** (`FUN_005323f0`, `Boundary_LayFresh`, with `[0x0081b4cc]` set): the walk replays `Hoardings.sam`'s clicks from
entrance B three on, `[0x007639f0]`, round to entrance A four back, `[0x007639f8]`, the gate's two ends. Each anchor's
`mDirection` is the way the walk leaves it (the last, the way it came); its mask names the anchors two cells away; a
straight over water or hidden ground carries flag 1 (row 11, `ho_fos4`). The model slots keep the walk's order (slot 3,
(51,17), to slot 109, (43,17), in Lost Kingdom), and the straights' variants follow it: in all 34 fresh Lost Kingdom parks
the original made (Alexah's and 33 under Proton), no two straights next in the walk share a variant, where in row order 31 of 34 have such a pair and in column order all 34; 27.4 variants a park on average (27.9 for one roll a straight in that order).
Everything else is the same in all 34: 107 anchors, 10 `ho_fob1`, 6 `ho_fob2`, one `ho_fos4`, 90 among the three
`ho_fos`. The variants differ from park to park, so the generator is seeded each time. How often the laying chooses an
anchor before the last time, so how far it moves the generator, is not read.
**The model:** `FUN_00522f20` (`Hoarding_MakePieceModel`)`( x, y, family, row, rotation, restoring )` turns the piece by the angle 360 less the rotation (360 folded to 0) and moves
the cell one on by that angle: 90 moves y, 180 x and y, 270 x (so a rotation of 90 moves x and one of 270 moves y), then makes the model with `FUN_00463060( table[row].model, 0x33a,
{ x, y, angle, 0x008179f8 }, table[row].mask, ..., -1, 0 )`: flags `0x33a` with a footprint, so the height is the middle of
the lowest and highest ground under it ("Where a placed object's model stands"), and the footprint at `0x008179f8` is
2 by 2 (`FUN_00522e40` writes both). The table `0x00763608` (`HoardingPieceTable`) is 28 rows of `{ model, name, mask }` that `FUN_00522e40` (`Hoarding_LoadPieceModels`)
fills from `%s\hoarding`, making each named row's model as item `0x4394 + row` (17300 on) and giving a row with no name the
row before's model (`0x00522ed2`):

    row   0 fos1  1 end1  2 fos1  3 fob1  4 fos1  5-8 (fos1)  9 fos4  10-11 (fos4)  12 fos4  13-14 (fos4)  15 fos4
          16-17 (fos4)  18 fos4  19 fob2  20 fos2  21-22 (fos2)  23 fos3  24-25 (fos3)  26 fob3  27 fob4      (ho_*.md2)

So a straight's variants 22 and 25 draw `ho_fos2` and `ho_fos3`, a model of items 17320 and 17323. The row's mask byte goes
to `FUN_00454a10`, whose `FUN_0044b140` hides (`0x80000010`) each node whose lookup record's `+8` word shares a bit with it;
none of the 36 hoarding models of the four themes has a lookup record, so it hides nothing.
**A loaded park makes no model of this again, but the loader claims each piece through its anchor.** Its model records
are made as they were saved (`FUN_004647a0`), and the anchor's track record names its piece's handle (FileFormats
`saves.md`, "A boundary piece's record"; the mask `+0xc` is the file's `mHoardingNeighbours`, the row and rotation its
`mTileData`). The post-load rebuild (`FUN_00415140`, `0x00415169`) calls `FUN_004d83a0`, which walks all 128 x 128 cells
(`0x004d83e3`..`0x004d83fa`) and calls `FUN_0053b1a0` for each: that clears the saved handle (`0x0053b1d2`, where the
cell's type is not 0x15) and, for family 5, calls `FUN_00522f20( x, y, {set, row, angle}, 1 )` (`0x0053b269`) and stores its
answer back. With the sixth argument non-zero `FUN_00522f20` calls `FUN_00463ab0` with no handle (`0x00522f66`,
`0x00522fa8`) instead of `FUN_00463060`: that walks the slots, skips any with `+4 & 0x10000`, and matches the item type,
flags `+0xe4` (`0x33a`), cell `+0xe8`/`+0xec`, angle and footprint; a match is marked `0x10000` and answered
(`0x00463c59`..`0x00463c6f`), else it answers 0 (`0x00463c70`). No model is made on either branch. Whether the original
then frees or hides a 173xx record no anchor claimed (a slot lacking `0x10000`) was not read. In the park files to hand
(933 anchors in seven files, 520 in the four distinct parks, across three themes) every anchor names a record of its row's
model, on its cell moved by its angle, at the record's angle (360 less the turn), and no 173xx record lacks an anchor.
**OpenTPW builds it** from the records (`ParkBoundaryHoarding`); a park made fresh chooses each anchor's piece once, in
the walk's order, with the chooser above (`FreshParkBoundary`, `ParkBoundaryHoarding.PieceFor`, `PieceOf`; Q656), its
straights drawing the fresh park's generator; the laying's draws before that are counted,
`FRESH_BOUNDARY_LAYING_DRAWS`. Measured: the stock park's wall right of the gate and along row 13 in both games at the
same cameras (Q620, `q620/overlay-gate-final.png`); a fresh park's in both games (Q656).

**Alexah's Full Simulation jungle drew none until Q810, and why (Q811, `q806/`, every prediction in its `PREDICTION.txt`, 7 of 7 held).**
`ParkBoundaryHoarding.Read` takes the records from `ParkThingStates`, the walk that stopped (until Q810) in her `New Save.TPWS` at 2058464 of
2101020 (a Temple of Gloom's 40 pylon records, item 1181, `StdPylon` made `0x3fa` by `Coaster_AddNode` (`0x00434276`) or re-claimed (`0x00434257`; `Coaster_SetupPylonModel`, `0x00430590`, makes only a measuring model it destroys at `0x00430a99`); its cars are item 1183, `park-gate.md`, "What draws the doors"; Q779), and answered none while the walk had a
problem. Her file holds 163 records of item 17300 on (`records.py`, `lib/parkfile.py`: rows 2 x 98, 3 x 10, 9 x 12, 19 x 7, 20 x 16,
23 x 19, 26 x 1), 82 of them in slots after the first 1181 record (a different measure from the stop, which lies after the second one's record, 2058182), so the walk's partial list could not hold the wall. Ours, main
`b8a71167`, her file's copy: `boundary pieces 0 anchors 163 unmade 163`, `BOUNDARY_HOARDING_UNMADE` 163, no wall at the gate's
view (`run1fs/gate_fs.png`); the stock park 143 of 143 (`run2stock/`); a probe reading item 1181 as 5 channels in the three load-side callers of
`ThingStates` (`ParkRides.cs:760`, `ParkObjects.cs:264`, `ParkBoundaryHoarding.cs:250`; a fourth, `ParkRides.cs:950`, writes): `pieces 163 anchors 163 unmade 0`, the wall laid round the gate like the original's, by eye at one camera (`run3probe/gate_fs.png`; the original's frame is Q809's `q805/orig/gate/o-t19017.png`, the same file and camera, taken
for Q809, not again for this; it is 1024 x 768 with the HUD and the wall, ours 1280 x 720 without the HUD). The count 5 was `rsys.py`'s reading; the original's is Q814's and Q810 built it: all four callers read, her copy `boundary pieces 163 anchors 163 unmade 0`
with the wall at the gate's view (`q810-a7/run3fs/gate_fs.png`).

#### A track piece's model, price and open end

Read for Q687 in Ghidra and measured in the original under Proton on `q581/karts.TPWS` (`q587/orig`, `original/trackcells.py`,
`q587/PREDICTION.txt` rounds O and W): a Dino Karts bought on (32,25) and looped south of its station as Q581's, then a
Splish Splash bought on (20,20) and looped north of its own, the other way round.

**The piece.** `Track_RetileCell` asks `Track_PieceForCell` (above) for a karts' track cell (0xb) out of table
`0x00762f30` (sixteen rows of `{ row | 0x30000, rotation, mask }`, count `[0x00700c18]`) and for a water track cell (0x10)
out of `BoundaryPieceRows` (`0x00762ff0`, family 4; only type 0x19's answer is made family 5): the same exact, then
subset, match by the neighbour mask, so rows 0 to 8 are none, an end (1), a straight (2), a bend (3), a crossing (4) and
the four junctions (5 to 8), at the boundary table's turns. Then a straight turns 180 more where its direction byte is
`0x40` or `0x10`; an end or a straight takes a hill row by its flags' low four bits (+9, +12, +15, or 0x12); a bend of
0xb or 0x10 adds 0x10, row 19, where (direction, mask) is (0x40, 0x50), (0x10, 0x14), (0x01, 0x41) or (0x04, 0x05), the
flow leaving it the other way round; and a water crossing or junction with direction 0x14 takes turn 90, with 0x41 adds
0x10, with 0x50 adds 0x10 and takes 270 (rows 20 to 24). The direction byte is the way the last run laid over the cell
left it. The stations (0xd, 0x12) and every other type go to the path table `0x00763138`, family 1, which makes no model;
a station cell is never retiled at all and reads family 0.
Family 3 makes the piece by `TrackKit_MakeKartPiece` (`0x00522b90`), family 4 by `TrackKit_MakeWaterPiece`
(`0x00522d50`), unless the cell carries the add-on flag 0x10 (the add-on is a placed thing with its own model, whose handle the walk's
section keeps: "The walk", above) or the row is 0: the
model of its row in the kit's table, `0x007633e8` (karts, 20 rows) or `0x007634d8` (water, 25 rows) of
`{ model, name, mask }`, which `TrackKit_LoadKartPieces` and `TrackKit_LoadWaterPieces` (`0x00522aa0`, `0x00522c60`) fill
from `%s\rides\gokarts` and `%s\rides\wateride` with load flag `0x10422`, as items 17100 + row and 17200 + row, a row
with no name taking the row before's model:

    row    1     2     3     4     5     6     7     8     9     10    11    12    13    14    15    16    17    18    19    20    21    22    23    24
    model  Q     S     B     X     (X)   (X)   (X)   (X)   H     (H)   (H)   H_U   (H_U) (H_U) H_D   (H_D) (H_D) H_A   B2    X2    (X2)  (X2)  (X2)  (X2)
    mask   0x01  0x11  0     0x55  0x15  0x54  0x51  0x45  0     0x01  0x11  0     0x01  0x11  0     0x01  0x11  0     0     0x55  0x15  0x54  0x51  0x45

The models are `gk_trck<model>.md2` and `wr_trck<model>.md2`; the karts' table stops at row 19, so rows 20 to 24 are the
water kit's alone, and row 0 is `-`, no model. A piece is turned and moved as a boundary piece's (360 less the turn; the cell one on by it),
flags `0x33a` with the 2 x 2 footprint at `0x008179f8`, and the row's mask hides each end cap whose lookup record's
`+0x08` bit is in it (`FUN_0044b140`; FileFormats `models.md`, "End caps"): a straight hides both, an end the one toward
its link, and a junction is the crossing with one cap left. A water piece is also put to clip 0xc, no role, as it is made
(`FUN_004732a0( model, 0xc, 0, 7, 1.0, 0 )`): it stands at rest (Q781, "The pieces' clips", below). Measured, every piece of both loops as the rule gives: the karts' ends
`gk_trckQ` at 90 and 270, straights `gk_trckS` at 0 (direction 0x01), 180 (0, direction 0x10) and 270 (90, direction 0x40), the
karts' four corners `gk_trckB`, row 3; the water loop's four corners `wr_trckB2`, row 19, at 180, 90, 0 and 270.

**The price.** `Costs.KartTrackCell` and `Costs.WaterTrackCell` (the `Costs` record at `0x007855fc`: queue, path, kart,
water, map cell; the `.sam` loader's setters at `0x004081b6` and `0x004081c4`, the getters `Costs_KartTrackCell`
`0x0052fb60` and `Costs_WaterTrackCell` `0x0052fb80`): 200 and 250 in Lost Kingdom's Instant Action, from
`jungle/Easy_Standard.sam`, which only game type 2 layers (`boot.md`); a Full Simulation park layers no Easy file and takes
`data/levels/Standard.sam`'s 400 and 500 (not measured); OpenTPW reads it through `ParkBalance`. `Track_StampCell`
(`0x00538fc0`) charges it for a piece laid on a cell not already its type and refuses one above the balance; over its
own type it is free and bumps the overlap counter `+0x20`; in mode 4, the put-down, and mode 0x3b (`[0x008186d4]` 1,
`0x00524b4a`, `0x005253e9`, and in the preview `FUN_005234d0` at `0x005235d4` and `0x00523967`, so the carried preview's
cost is free in the same two modes) it charges nothing, so the station and both mouths come with the ride's price. The preview's
verdict `Track_CellVerdict` (`0x00539760`) adds each such piece to the run's total (the yellow "$ N" under the money) and
refuses a run whose total passes the balance (`c_cash`) or whose pieces meet another ride's track, station or the
boundary. Measured: the put-down took 6500 (Splish Splash 5000) and nothing more; each click 200 (250) a new piece, the
anchor's own piece nothing.

**The refund.** Backspace with the track tool armed (on its release, `0x0040beaa`) runs `BuildTool_Backtrack`
(`0x0052fe50`): the pending list's last run is cleared in mode 0x35 (op 0x32, `Track_ClearCell`, `0x0053b280`), the
anchor goes back to the run's start and the tool stays; its walk pairs (`0x00530025`/`0x0053003e`,
`0x00530082`/`0x0053009b`) run `ShapeFromOtherMouth` with 1 only where `ShapeFromMouth` with 0 answered 0. A piece whose overlap counter falls below nought (or under the
sale's `[0x0081b46c]`, or forced, `[0x0081d7a8]`), a mouth only by the sale, is removed and refunds `FUN_004e2290`'s
percentage of its price (`0x0053b603`..`0x0053b627`), 100 for a ride with no customers under 30 days old; its cell reads
row 0x37, no piece. The sale clears and refunds every piece so, then withdraws two pieces' worth at the same percentage,
the two mouths that came free (`0x00528179`, `addresses.md`). Measured: a three-piece run taken back, +600, its first cell
an end piece again, the anchor on it.
Read for Q765 (`q765/dump`, `q762/dump/0053b280.asm`): `BuildTool_Backtrack` pops the list's top, the run's end (a list
left with one entry takes nothing back), lets the red latch go (`FUN_00527940( 0 )`), installs mode 0x35 and writes 0x80
into the last link step, then its two clicks: the first anchors at the end, the second lays op 0x32 from it to the new
top, the run's start, one pass of `FUN_0053aac0` (`0x00524960`'s mode-0x35 arm), so each anchor from the end back is
given the step toward the start; the anchor becomes the start and the tool its mode again (`FUN_0052f580`). Op 0x32 is
`Track_ClearCell( dx, dy, 1 )` (`0x00538cdc`): on a track anchor its counter `+0x20` falls by one, or with a step of
nought, but at the mouth `[0x007639e0]`, to −1 outright. A cell with one way on (`+0xd`), unless the mouth or the other
mouth (`[0x007639e8]`), lets go of its link back toward the cells already cleared (`~opposite( step )`), and a bend so
left with one link takes the way out of it, `opposite( link )`; a cell of two ways on (a crossing laid twice) loses that
way's bit and both links on the step's axis; either is retiled. Below nought, and no mouth, the piece goes as above, and
also: each of its links let go at both ends, a neighbour so left with one link and a way on that is neither the link nor
its opposite taking `opposite( link )`; an add-on's thing torn down (`FUN_00527ee0`); its way, flags, type and owner
nought, `+0x24` −1, retiled to row 0x37 of set nought (`0x0053b006`); and each of its two by two's cells naming it made
bare the same. On the mouth (`0x0053b8bb`..`0x0053ba7d`) it then links to a station of its ride two cells off where it
did not, with the way out of it, and with one link left takes the way away from the station. After the pass
`FUN_0052fc80( list back to one entry )` tidies the mouth the same way, lets go of a link to no track or to track that
does not link back, and back at the first entry breaks a bend at the mouth, letting go at both ends of the link not to
the station; then the walk pairs.
**OpenTPW** (Q765, `ParkTrackTool.Backtrack`, `TakeBack`): Backspace takes the last run back so, the anchor on its start
and the tool armed, each removed piece refunded at its whole price (the scrap percentage is counted,
`SCRAP_VALUE_DEPRECIATION`), no pieces' clips (Backspace calls neither clip walk: Q781, "The pieces' clips"), the pulse as the walks that follow give it (Q767). Measured in ours on the final
binary (`q765/ours-b`), by the original's clicks: the second run taken back, 3 pieces, +600, (34,29) links 0x01 way 0x10
over 0 piece 3/1/180, the three others bare at row 55, as `q587/orig/A-load.log` at 199.64 s (earlier evidence); the
run laid again and the loop closed as Q763's.

**The open end's pulse.** `TrackRide_ShapeFromMouth` (`0x0052c6f0`) walks from the mouth (x + 4, z + 1 at turn 0,
heading 4; 1, 0x40 and 0x10 at 90, 180 and 270) and `TrackRide_ShapeFromOtherMouth` (`0x0052ccb0`) from the other mouth
(x − 2, z + 1 at turn 0) heading away from the station, the circuit's way back walked backwards, each in 2-cell steps
along the laid track, out of a bend the way its mask leaves, until a cell with no link onward, or the station met from
its far side (answering 1). A cell with hill flags is reshaped by its neighbours (hills on both sides 4, on neither 1,
ahead only 2, behind only 8) and retiled, with clip 0xc or 5 when asked. The cell it stops on with no link onward is
pulsed when the second argument is 1 (`Model_PulseOn`, `0x00469580`: model flag `0x4000000`, the list `[0x007afe14]`)
and stops pulsing when it is 0 (`Model_PulseOff`, `0x00469650`). `Model_PulseTick` (`0x00469840`, once a drawn frame from
`0x0054e301`) moves a phase by twice the frame's seconds, wrapped at 2, and writes its triangle, 0 to 255 and back,
into every vertex colour of the pulsed models as alpha and red 255 and that value for green and blue: red to white and
back once a second. The tool's arming (`0x00526c89`) and each click that moves the anchor (`0x0052718c`) call
`ShapeFromMouth` with 0 and `ShapeFromOtherMouth` with 1, so while the loop is open the other mouth, where the circuit
must come back in, pulses; every click's op 0x86 runs the other pair first (`0x00538ca0`, `ShapeFromMouth` with 1, then
`0x00538cb7`, `ShapeFromOtherMouth` with 0 where it answered 0), which the click's own pair then flips. At the put-down
`FUN_00524960` calls neither walk itself: the only walk it reaches is the mouths' op 0x86 (applied at both mouths), so the
mouth pulses until the tool is armed (not observed by a breakpoint); a closed loop pulses nothing. Measured: the mouth after the
put-down, the other mouth from the arming click until the loop closed (`q587/orig/B-3-run1.png`, red on the left), nothing after.
Read for Q767 (`q767/dump`): a walk turns out of a bend by its links less the way in, `opposite( heading )` where the bend
links that way, else the heading itself; it pulses only a cell whose piece has a model (`+4`). `Model_PulseOn` sets the
model's flag `0x4000000` and puts it on the list; `Model_PulseOff` takes it off and copies the shared mesh's lit vertex
colours back. The tick takes its value at the phase before moving it on: `p` up to 1 and `2 − p` above, times 255 and cut
by `__ftol`, written as `0xffff` and the value twice (alpha and red 255, green and blue the value). The pairs and their
values: the put-down's op 0x86 (1, then 0 where open), the arming (`0x00526c89`, 0 then 1), a moving click (`0x0052718c`,
0 then 1), the close (`0x00526f3c`, 1 then 0), Backspace (`0x00530025`, `0x00530082`, 0 then 1 where open, both
branches), the closed loop's two-click edit (`0x00526b7a`, 1 then 0 where open) and `FUN_00532fc0`'s (`0x0053426f`,
with the clip argument).
**OpenTPW** (Q767, `ParkTrackTool.Shape`, `ShapeMouths`, `ParkTrackPieces.SetPulse`): both walks, with every pair but
the two-click edit's (not built, `TRACK_EDIT_CLOSED_LOOP`) and `FUN_00532fc0`'s; op 0x86's pair runs once for a run, as
every cell of it asks the same of its owner. A pulsing piece's every vertex takes (255, v, v) in place of its light
(`ModelEntity.VertexColour`, the shader's `g_vVertexColour`), on the frame clock (`Time.Delta`). The hill's reshaping
by the walks is counted (`TRACK_WALK_HILL_SHAPE`).

**The pieces' clips** (read for Q781 from `q781/dump`, `0052a490.asm` and `0052a700.asm`, and the callers in
`testme.objdump`). `TrackRide_PlayPiecesFromMouth( ride, f )` (`FUN_0052a490`) and `TrackRide_PlayPiecesFromEnd( ride, f )`
(`FUN_0052a700`) take the ride thing's cell (`+5`, `+7`) and turn (`+0x10`) and start, like the mouths' walks, from the
mouth on the turn's heading (0: x + 4, y + 1, east; 90: x + 1, y − 5, north; 180: x − 5, y − 2, west; 270: x − 2, y + 4,
south) or from the other mouth away from the station (0: x − 2, y + 1; 90: x + 1, y + 1; 180: x + 1, y − 2; 270: x − 2,
y − 2, each on the opposite heading, `FUN_004d8c00`); the two bodies are otherwise the same. Each step, in 2-cell steps: a
cell that is not track (`TrackCell_IsTrack`) sets the heading to nought; on track a bend (`TrackCell_BendLinks`) turns as
the mouths' walks do (its links less the way in, `opposite( heading )` where it links that way, else the heading), and a
cell whose record names a model (`+4`, so not an add-on's cell nor a station) has it played,
`FUN_004732a0( [0x007a4610 + handle * 4], f ? 5 : 0xc, 0, 7, 1.0, 0 )` (`0x0052a5bb` and `0x0052a5de`; `0x0052a833` and
`0x0052a856`): role 5, the `M` clip, looped and started at once (flags 7), or 0xc, which names no role, so the rest pose
(`FUN_00472f60`, "A trigger naming a role the model lacks", above; a model with no `M` clip rests under 5 too). The walk
stops at a cell that is no bend and links no further on, and where the cell two on (`FUN_0050c3e0( heading, 2 )`) is a
station (`FUN_0053adb0`) of the same owner (`+0x10`) linking back. The callers, each after the circuit's walk:
the closing click (`0x00526f56`..`0x00526f7f`) passes f = 0 always, from the mouth alone where the walk answered 1, else
from both; an add-on's put-down (`0x00524d19`..`0x00524d47`) and the closed loop's two-click edit (`0x00526be4`..`0x00526c0d`)
pass 1 from the mouth alone where it answered 1, else 0 from both; `BUMP 17` (`0x00554a1a`..`0x00554a47`, the table above)
walks both with 1 for an operand of 0 and 0 for any other. The mouths' walks (`TrackRide_ShapeFromMouth`, `TrackRide_ShapeFromOtherMouth`) play the same clips when their third argument is 1: role 5 at `0x0052ca82` and 0xc at `0x0052cbe3` (from the mouth; the gate is `[esp+0x30]` at `0x0052ca4e`, the choice `[esp+0x34]`), both at `0x0052ce84` (from the other mouth; gate `[esp+0x34]`, choice `[esp+0x38]`). Every track-tool caller passes 0 (`0x00526c89`, `0x00526c9c`, `0x0052718c`, `0x005271a0`, `0x00526f3c`, `0x00526f4e`, `0x00530025`, `0x0053003e`, `0x00530082`, `0x0053009b`, `0x00538ca0`, `0x00538cb7`); only `FUN_00532fc0`'s op 0x86 arm (`0x005341a2`) passes 1, at `0x0053426f` and `0x00534285`, with f = the ride's `+0x2c`, and which callers apply that op to a ride's track cells was not traced. A water piece is put to 0xc as it is made ("A track piece's model, price and open end", above), so every
piece stands at rest until a walk with 1 or `BUMP 17 0`; Splish Splash's script runs `BUMP 17 0` at @33 as it starts a go
and at @219 after a repair, and `BUMP 17 1` at @155 and @201 when it shuts or breaks (`Wateride.RSE`), so among the callers read the
original's water flows while its ride runs, after an add-on, or after a loop's edit. Of the 53 `M` clips the eight kits ship, 48 carry UV tracks alone and five
no readable span (Haunted Hallow's four `gk_trckH*m`, Space's `gk_trckh_am`); Lost Kingdom's and Fantasy's karts ship none,
so of Lost Kingdom's track only the water moves (FileFormats `models.md`, "Which animation file a model takes").
Measured in the original (Q781, `q781/orig/O3.out`, a Splish Splash on (20,20) of `q581/karts.TPWS` looped south): no clip
call from the arming through two runs (`O-brkA.log`); the closing click's 8 calls all at `0x0052a5de`, their handles 93,
95, 96, 169, 170, 171, 172 and 94, `trackcells.py`'s at (22,19), (22,21), (22,23), (20,23), (18,23), (16,23), (16,21) and
(16,19), the loop in order from the mouth (`O-brkB.log`); none other in the 35 s after, and the water still in two frames
1.4 s apart (`O-9-closed-a.png`, `-b`, `O-crops.txt`).
**OpenTPW** (Q781, `ParkTrackTool.PlayPieces`, `PlayPiecesAfterWalk`, `WalkAfterAddOn`, `ParkTrackPieces.PlayClip`): both
walks, after the closing click and an add-on's put-down; a piece is made at rest, and clip 5 runs its `M` clips through
`MeshAnimator` on the park's clock, restarted from frame 0. Not built: the closed loop's edit (`TRACK_EDIT_CLOSED_LOOP`) and
`BUMP 17` (Q448: a kart or water ride's every `BUMP` is counted here, so Splish Splash's water never flows by its script;
whether a loaded park's pieces take their saved model records' channel in the original is not read: after a load the script's `BUMP 17 0` is skipped by its `+0xe6` test, which the record keeps (`saves.md`, "A script's record"), and ours makes every loaded piece at rest).

**The carried ride's first track cells** (`TrackRide_CarrySquares`, `0x005286a0`, from `BuildTool_Placer` at `0x00529c65`
and `0x00529ce1`): each mouth's 2 x 2 cells, by op 0x10b (karts) or 0x110 (water) with step 5 for the preview's squares,
and 0x20b or 0x210 for the commit's check, whose refusal refuses the placement. Seen: the carried Splish Splash with both
mouths' squares and their arrows (`q587/orig/W-1-before-put.png`).

**OpenTPW** (Q688, `ParkTrackPieces`): the put-down lays the station and both mouths, each anchor's way on the turn's
heading (east, north, west, south for 0, 90, 180, 270), the station's two linked both ways, the mouth back to the station,
the other mouth on to it, the owner the ride's cell, the 2 x 2's other cells 0xc in both kits naming their anchor; at turn 0 from
(30,24) the four cells read as the original's at its put-down (`q587/orig/A-load.log`; `q688/ours-b`), and at turn 180 as
Alexah's played Dino Karts lies. Each kart or water anchor draws the piece its record's row and turn name, its end caps
hidden by the row's mask: Alexah's played file's 28 pieces are its 28 records' (`q688/ours-f2`), and the chooser
(`ParkTrackPieces.PieceFor`) gives every one of its 31 anchors the row and turn the file holds. A sale of a ride owning only
its station and mouths clears them and pays nothing net. Counted, not built: the carried squares and their check
(`CARRY_PREVIEW_TRACK_START`), a
written park's piece records (`SAVE_PARK_TRACK_PIECE_MODEL`), and the sale of a ride with more track
(`SALE_TRACK_TEARDOWN`). The drape is built (Q808), and the interpolating pass of the rides' and add-ons' own meshes (Q815); no piece carries such a mesh. The put-down's check of the mouths (op 0x20b or 0x210) is not built: an anchor whose two by two holds
track already is not laid, and counted (`PUT_DOWN_TRACK_OVER_TRACK`, Q765). The pieces are stood again after a change
keeping each model its anchor still names, so only a changed piece is made again (Q765). The track tool's runs are Q762 ("The track tool", above), the circuit's walk and the opening
Q763 ("The walk", above); the rest is Q587.

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
`+0x216f0`, capacity `+0x216f4`, wrapped flag `+0x216f8`; `hud.md`, "The arrivals", and kept here, Q396). While the vehicle reports state **2** it
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
`saves.md`, "The arrival block"). Entering a park zeroes `mGameTick` (`FUN_005156a0`, `0x00515865`, from `FUN_00407e00` at `0x0054ed3f`).
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
**`mRandomSeed`** (`+0x1da708`), the save's own seed. The spent load (`0x004cf5a5`, through the call at `0x004cf4b1`) and the tail pass 0 (`0x004cf4f2`, `0x004cf50c`,
`0x004cf526`, `0x004cf54a`), but a current vehicle is reused whatever the argument, so the random arm is reached only
from the tail's call when no vehicle is current (`0x004cf526`). The draw is `FUN_00516330`'s inlined, the state written
back, and `% 3` unsigned: nought the bus, one the seaplane, two the ferry (`0x0051a34b`..`0x0051a37f`, an unsigned `DIV`).

**OpenTPW** (Q443): the park's generator is `ParkState.RandomSeed`, seeded from the file's `mRandomSeed` and written
back to it by the save; `ParkPeople.Summon` draws it for the random arm and nothing else draws it, as every other system
here keeps a generator of its own (`ParkGenerator` says so, each site where it departs). From the stock Lost Kingdom's
`0x3f416bba` the first four draws pick the bus, the seaplane, the bus and the bus. The original's state at the same summons
is not ours: its needs turns, its scripts' `RAND`s and the other systems draw it between, and each arrival reseeds it with
the guest's id.

The save agrees from the other side: its header fields are named `mArrivalVehicle_Size1`, `_Size2`,
`_Size3` and `mCurrentArrivalVehicle` (`FUN_00516c80`), and `FUN_0051a2f0` caches the three at
`+0x1da72c`, `+0x1da72e`, `+0x1da730` with the current one at `+0x1da72a`.

**The vehicle thing is made on demand.** Where the slot is empty, `FUN_0051a2f0` looks the feature up
by name, allocates `0x450` bytes, calls `FUN_004db090( 1, <name>, 0, 0 )`, takes the id from
`FUN_0050b350` and caches it. If the pick is unavailable it falls back through the other two by
bitmask (measured: the slot words change as it is made, and the analyser's newest-build stamp with them,
`advisor-park.md`, "What moves it with nothing bought"), and if all three fail it dies with `"Fatal error: Could not find either the bus or the plane
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
(either, unless ten or more are still to drop: `[+0x2d0]`, `0x0051a83d`) or moves on (status 3, tested first at `0x0051a833`, whatever is still to drop); then a ticket booth by `draw & 1` (`FUN_004d8610`), a point on
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
load then finds. When the wanted feature is missing `FUN_0051a2f0` falls back through the ferry, the seaplane and the
bus, a slot that holds a thing taken as it is, and so does `ParkPeople.Summon` (Q26c; every theme ships all three, so no
shipped park reaches it). **Not the original's in one way**: with none of the three to be had the original sets no
vehicle current, and here the one asked for is current with no thing, so its guests are still made at its cell.

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
leaver stands there, at 0 when one does, and at 2 when one does and the load is all dropped; before the tail, the
manager itself triggers a vehicle at 2 on the sweep that finds its held load all dropped, leaver or none, as it lets
the load go (`0x004cf599`, size 0 through the call at `0x004cf4b1`)
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
of nobody and still sends its vehicle (neither reached in Lost Kingdom; a load that would pass the cap is cut to what fits, counted here on everybody and not on `FUN_004c7fa0`'s crowd); a load saved half-dropped is carried on by its count and flag, and by the vehicle the save names current where that is one of its three sizes' things (`saves.md`, "OpenTPW's writer, the arrival vehicle"; another thing is counted, `SAVED_CURRENT_ARRIVAL_VEHICLE`, and the rest then come by the vehicle their number summons); and guests made with no script to ask, on the sweep after the one that calls the load. A made vehicle's id is the
free list's head, the id last freed, as the original's (Q582).
(The second load in the older measurement above dropped on the sweep that called it because the spent bus was then
sent round again: Q131b took that out.)

**A vehicle the file names no thing for is made whole at its first summons** (Q26c; Q342 made the thing). Nothing of it
stands or runs before: `ParkFixedItems` stands only the vehicles the file names. At the summons the object is made on
cell (0,0) (`saves.md`, "OpenTPW's writer, a vehicle made at its summons"), its script bound from its top through
`ParkRides.BindNew`, as the object constructor binds a bought thing's, given `VAR_STATUS` 1 and no trigger
(`0x0051a5ed`), and its model stood just after the frame's entity walk (`ParkFixedItems.Sync`; made inside the walk,
its entities stopped the game, "Collection was modified"). Measured (`q26c/`, each predicted first): main's build stood
the ferry and the seaplane at the load and drove both into the harbour with nothing summoned, the ferry at status 2 76
ticks after entry (`run-A2/A2-docked.png`); this build stands neither (`fixed item 'ferry' is not made, catalogue
1604`), and `load 61` made the ferry thing 43 on its next sweep (764), status 1 and trigger 0, its first guest at
(53,3) on 845, 81 ticks on; `load 40` made the seaplane, its first guest 48 ticks on. The original given
`q340/late30.TPWS` (`q26c/orig2`, `orig3`) made the seaplane on 1094 (thing 14, `size2` and current), its script
reading trigger 0 and status 1 from the first poll, 2 on 1141 (47 on), 3 on 1143, 6 on 1222, forgotten on 1223; Q341's
run of the same file made the ferry. **The vehicles stop where the original's do** (Q583;
"Where a route key puts its node"): at status 2 the seaplane lies level at the harbour's top middle, nose left, its
red and yellow 1.2 px from the original's (`q583/side-by-side-seaplane.png`), and the ferry at its top right of middle at
the original's slant (`side-by-side-ferry.png`, the original's ferry from `late30` with seed 1).

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
`WhichTrackType` at `+0x9c`, both known from their readers. So **two of one flat ride are worth what one is, less the division's remainder**, each
adding half cut to a whole number (35 gives 17 twice, 34), and all of them stay new, or stop being new, together, by the day the first was built, a sold one
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

## A park's particles, started and drawn

Q258. The particle system is the lobby's (`lobby.md`, "Particle system"); this is what a park adds: who starts an
effect in the world, and how a world particle becomes a sprite.

| Address / value | Name | What it is | Evidence |
|---|---|---|---|
| `0x0051ef30` | `Particles_Render` | Packs one emitter's particles into 28-byte sprite records. Its world arm (the effect's `+0x05` nought): each coordinate times 16, the size word doubled (`<< 1`, kept as sixteen bits), the effect's draw flags with `0x4000` | Decompiled |
| `0x0057c620` | `SpriteBatch_DrawParticles` | The sprite pass over all 120 emitters. A record without `0x40000` has its place over 1024 put through `FUN_00578d20` (case 0) with the matrix `[0x0087b088]`; the quad's corners are then set out round that point: half height the size over 1024 times 0.5 (`0x00701ac8`, `0x00701b28`), half width that times the picture's float at `+8`, or `DAT_008bcbcc` with no picture; turned by the rotation word over 65536 (`0x00701ad0`) | Decompiled; the call's `ECX` read in the listing (`0x0057c875`) |
| `0x0087b158` | | The matrix `[0x0087b088]` names (`FUN_005781d0`). `FUN_005783b0` builds it: the camera's matrix `0x0087aff0` inverted, times the projection `0x0087b110`, times `0x0087b0d0` | Decompiled; all three read in the running original |
| `0x0087b0d0` | | A scale: `2 x DAT_008bcbcc` across, `-2` down (`FUN_005781d0`: `0x0087b0e4 = 0xc0000000`). Read live: 1.5, -2, 1, 1 | Decompiled; memory |
| `DAT_008bcbcc` | | 0.75 in the running original at 1024 x 768: the screen's height over its width | Memory |
| sprite record `+8` | | A picture's width over its height: 1.0 for set 8's 61 x 61, 1.0357 for its 58 x 56 (`Sprites_LookUp`'s 24-byte records) | Memory |
| state bit `0x800` | | Set for every record carrying `0x4000`, so every world particle: no depth write (`render-states.md`). A person's sprite carries no `0x4000` and writes depth (`park-engine.md`, "What hides a person below their feet"). Draw flags `0x4` give the word `0x200522`, with `0x2000` plus `0x30` (source alpha, one) | Decompiled |

**The matrices, read in the running original** (`orig/look.py`, the park camera on a bought Drinks Shop): the
projection is `diag( 0.70711, 0.70711, . )` with the fourth taken as 0.70711 of the depth, a 90 degree lens
(cos 45 and sin 45); the third matrix scales it by 1.5 and -2. A point's `x / w` and `y / w` after the product
are the screen's -1 to 1: the bought shop at (450, 6, 342) comes out 587 px across of 1024, and stands there in
the frame; with the screen at -2 to 2 it would be at 550.

**So the sprite's size in the park** follows from the matrix alone, because the corners are added after it: a half
height of one is `1 / (2 cos 45)` = 0.707 park units, a half width of one `1 / (1.5 cos 45)` = 0.943. A bubble
(effect 58, size 1024, so a half height of 1.0) is 0.707 units up and down, 5.4 to 5.9 px of 768 at the reference
view's depth of 65 to 71 units, where the original's own frame shows its rings about 11 px tall. A picture is so
drawn a third wider on the screen than it is authored, as the lobby's are. **Not settled by the picture:** that
width. The frame at 1024 x 768 shows a bubble's ring too faintly to tell 13 px from 10 (`orig/overlay.py` marks
both on it); the rule is the listing's.

**What follows for the lens:** a product of `1.5 x` and `2 x` over a 90 degree projection draws a picture 53.13
degrees top to bottom, not 90, and it is every view's (`park-engine.md`, "The lens: 90 handed to the projection, 53.13
drawn"; Q260). OpenTPW's park camera draws the same 53.13 (Q441); the sprites here are built at their size in the
park, so they are right under either lens.

**Who starts one.** `FUN_005573d0` (`ADDOBJ`'s and `EVENT`'s worker) cases 1 and 2 call `Particles_Spawn` and
`Particles_SpawnFull` with the node's place times 1024 (`saves.md`, "OpenTPW's writer, an emitter started" and
"a particle with a direction"). The loader keeps a file's emitters and empties their particles (`FUN_0051f7a0`).

**Measured in the original** (`q258/orig/`, Q257v's file loaded under Proton): the Drinks Shops' bubbles hold 9 to
15 particles each and reach 38.4 and 42.2 units over their emitters in 40 s (27.4 to 33.8 in a ten second look:
the highest seen grows with the time looked), 4.8 aside; the toilet's stink (69) 9.4 over and 8.7 aside.

**What OpenTPW does.** `RideScript.StartParticle` starts a type 1 or 2 in the park's `ParticleSystem` at the
node's place cut to whole units, a type 2 aimed (`Emitter.Aim`: the direction times the effect's speed over 1024
toward nought, no draw for the emitter's own velocity), and keeps the handle on the record
(`RideEffects.Record.Emitter`); `KILLOBJ` and a script's end kill it. A load starts again the emitter each
particle record names in the file, at the file's place and aim (`RideScript.StartRestoredParticles`).
`WorldParticles` draws every emitter not flagged for the screen as quads in the world turned to the camera, the
size above, blended or added, depth tested and not written. The console's `particles` prints the running
emitters, their particles and how many were drawn; `emitters` is still the loaded file's.

**Run, 2026-10-10** (`q258/confirm.py`, 4 of 5 predictions on the desktop). The shipped shop's bubbles carried on
at (440,6,312), a bought shop's started at (450,6,342), a worn toilet's flies and stink at (555,10,174): four
emitters, the bubbles holding 10 to 12 and 11 particles, every particle drawn; the shop sold, its emitter was gone
inside seven seconds; the save still wrote its three. The bubbles' highest read 42 and 41 units in 25 s, the
original's 42.2 and 38.4 in 40 s; the prediction's band (26 to 36, taken from a twelve second look) was wrong.
`bubbles-opentpw-left-original-right.png` is the two games' shop.

**The sweep carries each emitter with its node** (Q555; `RideScript.SweepEffects`): every tick, for every script
with a record, due a turn or not (`0x005516f6` jumps a script not due straight to the list walk at `0x0055188f`), a
particle record with a node has its emitter put where the node stands, cut to whole units (`0x0055190f`), and a type
2's aimed again (`0x00551943`). A node a clip carries is taken from the thing's players as its drawn model is posed
(`RideNodes.Follow`), so the End's trails go with their planes (`q555/run1`). The Jungle Spray's node 2 moves under
none of its clips but the build clip (`q555/spray-probe.out`, which asked that node alone), so that jet stands where it
was started. Not measured: its nodes 1 and 3, which `Junspray.RSE` starts effect 37 on too (words 59 and 183), and the
other carried records in Lost Kingdom, the Laughing Hyenas' on nodes 4 to 6, the rides' effect 16 smoke nodes, the
torches, the Volcano's, the Inca God's, the gift shop's, and the type 1s of `lavajump`, `Coconut`, `Staff`, `watertun`
and `5x5rck`: which of them a clip moves is not known, and moving them is the engine's behaviour.
**Not the original's:** the engine's lookup record holds the pose of the frame the model was last drawn in, so an
emitter on a model off the screen stands still there, where this follows the players. A record whose lookup record has
no matrix is started at the origin (`RideScript.StartParticle`; a type 2 aimed along (0, 0, 1)) and passed over here,
so it stays there, as the engine's, which moves it to the origin every tick (`0x005518f9`, the float at `0x00700fd8` is
nought). A node a clip's route moves, or one under it, is read where the route puts it, as `RideNodes.Follow` poses
through `NodePoser` (Q561, Q583); a carried find of such a node is still counted `RIDE_NODE_ON_A_ROUTE`, a count that is
stale (Q573): every Lost Kingdom run reaches it at its vehicle's sound start (the bus's `mk_bus_1` at its route's
frame-0 point, the seaplane's `mk_plane_1`, the ferry's `mk_ferry_3`).

**Deviations and gaps, each counted where it is reached.** Started nowhere: an effect of the item's own
(`PARK_PARTICLE_ITEM_EFFECT`), a record with no node or one the model lacks (`PARK_PARTICLE_NO_PLACE`), a type 2
whose node's matrix is never stored (`PARK_PARTICLE_NO_DIRECTION`), a start the system refuses, whose record the
engine frees and this keeps (`PARK_PARTICLE_NOT_STARTED`). A loaded emitter begins its effect again: the file's
running words are not read (`LOADED_EMITTER_LIFE_PART_RUN` where its life is not the template's). An effect added
without its alpha (flags `0x4` with no `0x2000`) is drawn scaled by it (`WORLD_PARTICLE_ADDED_WITHOUT_ALPHA`).
Not started at all, and counted under their own keys: a rider's puffs (`BUMPER_RIDER_PARTICLE`) and a leaver's (`LEAVER_BOARDING_PARTICLE`) (a broken boat's smoke is started since Q259, below). The
writer is as it was: an emitter is written as it starts, not as the running one stands.

**The density is the park file's** (Q703). The loader copies the file's whole live header over the system's
(`FUN_0051f7a0`, `0x0051f857`), the density at `+0x20` with it, so a park read from any file (Instant Action's
`Easymode.TPWI`, a save, `restart.INTS`) starts every effect at the file's density, not the options': 500 in the
shipped park and the files saved from it, 1500 in Alexah's played Full Simulation saves (`lib/parkfile.py`'s
`particle_density`). A park made fresh reads no file, so OpenTPW keeps the options' for it; that the original's
fresh park does the same is not traced (the 1500 of those saves is `high.sam`'s, which agrees). A rate of -2 at 500 is -1, so the End's trails
(93 to 95, rate -2, most 500) make one star a tick, at most 244, where the options' 1000 left them one every two
ticks. OpenTPW takes it as the park file is read (`Level.ReadPark` calls `Level.TakeParticleDensity`,
`ParticleSystem.TakeFileDensity`; the log's "Particles: jungle's park file holds density 500"), as the original's file
read calls its loader (`0x00415437`), so it stands before anything of the park starts an effect; a test holds the call
in the read (`ReadingTheParkTakesItsDensity`). **Measured** over `q550/alldone576.TPWS` (`q703d/PREDICTION.txt`):
the original's live `[0x0080cef0]` 500 and its trails at rates (-1, -1, -1, -1), most 244, 90 to 106 stars each once
5 s old (`original/emitters.py`); ours before at 48 to 54, after at 98 to 107 with the same rates and most (the
console's `particles`, which prints the density and each emitter's rates and most); the stars' sizes the same in both
(`particles <slot>`, `original/particledump.py`; `q703d/side-by-side.png`).

### A broken boat's smoke

Q259, split by the session: the item's other pieces are Q261. The decode is `saves.md`, "OpenTPW's writer, a
smoking car": a break starts effect 2 (`Smoke`: one particle a tick, each living 60) at each flagged car's
emitter node (`FUN_00544c80`, `0x00544dd8`), the car's step moves it there every track tick (`Bumper_StepCar`,
`0x00548587`), and a fix or the car's removal kills it (`FUN_00544e50`, `0x0054b077`).

**What OpenTPW does.** `ParkBumperBoats.Sync` asks `ParkBumperBoats.Smoke` for each boat it stands: while the
car smokes, effect 2 is started in the park's particle system where the boat's emitter node of id 2 is drawn
(each of the three times 1024 and cut), and moved there on every frame after; once the car smokes no more, or its
boat is let go, the emitter is killed. A car a file held smoking smokes from the park's first frame. A boat whose
model has no such node is counted once (`BUMPER_CAR_NO_SMOKE_NODE`; no Lost Kingdom boat), and a start the system
refuses each time it is tried (`PARK_PARTICLE_NOT_STARTED`). `BUMPER_CAR_SMOKE` is gone.

**Deviation, said at `ParkBumperBoats`:** the smoke is started on the first frame after the break and moved a
frame at a time to where the node is drawn, eased between ticks as the boat is; the original's is started in the
break and moved once a track tick to the node's place on that tick.

**Run, 2026-10-10** (`q259/confirm.py`, 5 of 5 on the desktop at the second run; the first's one miss was the
script's own pattern). A Hot Pot bought, filled and broken in its go: four log lines `smokes`, and 2.5 s on
`particles` read four emitters of effect 2 at height 30 over the pot holding 56, 58, 59 and 60 particles, every
particle drawn, seven emitters of the world in all (the shop's bubbles, the pot's own two of effect 16); the save
wrote four cars' smoke as before; the mechanic had it mended 11 s on (23 s in the first run) and five seconds
later no emitter of effect 2 was left. **The control** (the tip before): no emitter of effect 2,
`4x BUMPER_CAR_SMOKE`. **The original**, its own pot (`hotpot-later2.TPWS`) broken 3.5 s after the load's click:
four emitters of effect 2 at height 30 holding 59, 61, 58 and 60 five seconds on, and 60, 60, 60 and 62 fifteen
on; its frame then shows grey smoke over each boat, as OpenTPW's does (`smoke-opentpw-left-original-right.png`).
**A load**: the park entered on the original's own broken file read four emitters of effect 2 at (442,30,263),
(431,30,268), (424,30,261) and (422,30,251), the file's own places to the unit (`q259/loaded.py`).

**Not looked into:** the original starts four more emitters inside half a second of a break (effect 19 at the
boats, the riders' puffs, `BUMPER_RIDER_PARTICLE`).

### The sprite pass, and the Hot Pot's own smoke

Q261, split by the session: the item's other pieces are Q262. The question was Q259's: the pot's own two smokes
(effect 16, `Smoke2`, its script's `ADDOBJ 2` at a break) ran here and were not seen in the frame.

| Address / value | Name | What it is | Evidence |
|---|---|---|---|
| `0x00576a00` | `Render_DrawView` | A scene's frame. The six model lists are walked first; `SpriteBatch_DrawParticles` is called after them (`0x00576cbd`), handing each particle's quad to the batches; `FUN_0058a660` and then `FUN_00582170` draw | Decompiled |
| `0x00582170` | | `DrawList_Flush` (`0x00581db0`), then `FUN_00582ad0`: the per-batch sort, furthest first (`render-states.md`, "Three back-to-front sorts") | Decompiled |
| `0x0051ef30` | `Particles_Render` | A particle's colour word goes into its sprite record as it stands, whatever the effect's draw flags | Decompiled |
| `0x0057c620` | `SpriteBatch_DrawParticles` | An effect without draw flag `0x4` takes its state word from its picture's texture (`Texture_BlendStateWord`), so `0x2000` alone changes nothing in the draw | Decompiled |

**The two clouds are the same** (`particledump.py` on the original, `particles <slot>` here; `q261/cloud.py`).
The original's own pot broken in a go, seven seconds on: 117 and 120 particles, 12.6 units out from the emitter
at the far end and 4.4 at the middle, up to 22 high, sizes 1222 to 3500, ten alphas from 0 to 255. Here, three
seconds on: 103 and 102, 9.9 out and 4.45 at the middle, up to 18 high, sizes 1216 to 3407, the same ten alphas.
Nothing was wrong with what runs.

**Why it was not seen, two things.** Q259's frame of OpenTPW was taken from the far side of the pot
(`camera 435 320 110 180`), where the original's file looks from the near one: from that side, OpenTPW yaw 0,
the cloud at the doorway was there before any change. And a real fault beside it: the particles were drawn in the
see-through pass in the order entities were made, writing no depth, so the see-through half of anything made
later (a bought pot's sign, its doorway and its hoardings) was drawn after them and painted over the smoke
standing in front of it. The smoke showed only through the doorway, cut off at the sign's edge.

**What OpenTPW does.** `Entity.RenderWorld` draws the world a pass at a time: every solid half, every
see-through half, then the sprites (`Entity.OnRenderSprites`), where `WorldParticles` now draws. **Deviation, said
at `Entity.RenderWorld`:** the original sorts its batches furthest first, sprites among them; nothing here is
sorted, so a sprite standing behind a blended surface that it shows through is blended in the wrong order. The
people's sprites are still drawn in the see-through pass.

**Run, 2026-10-10** (`q261/confirm.py`, the desktop). A Hot Pot bought, filled and broken in its go, three
seconds on, `camera 435 228 35 0`: `particles` read two emitters of effect 16 at (434,0,233) aim (0,7,-30) and
(434,1,276) aim (0,11,28) holding 103 and 103, every particle of the world's 455 drawn. The sign over the
doorway, the frame's box (590,200)-(690,262), read a mean of 52.2 and 57.7 in two runs of the fix and 64.7 and
64.5 in two of the control (the particles back in the see-through pass): the smoke lies over the sign, the
doorway's posts and the hoardings in the fix's frame and is cut off at their edges in the control's
(`sign-fix-left-control-right.png`). **Predicted and missed:** the mean under 55 (57.7 in the second run) and
the box's dark pixels doubled (1.7 times, then no more than the control's: the count was of the sign's lettering,
a poor measure). From the fire arch's side (`camera 435 270 60 180`) the far cloud is a wisp at the arch's mouth
in the frame, not the cloud in front of the logs that was predicted.

**Against the original** (`foot-opentpw-left-original-right.png`, its own file `hotpot-later2.TPWS` broken 3.5 s
after the load): both show a dark cloud at the pot's doorway. The original's is wider on its screen and blacker.
**Not settled:** how much of that is the lens (Q260: everything stands larger in its frame) and how much the
blend. Set 10's eight pictures are dark grey (a mean of 32 to 42) and faint, no alpha over 121 of 255 and half
of each under 25 (`spritesheet.py`); which of `Texture_BlendStateWord`'s words their texture carries in the
running original was not read.

### The land's own particles

Q262, split by the session: the item's other pieces are Q263. The shipped park's emitter of effect 20 at
(532,4,526), which no script's record names, is the land's own: the level's `scape.omp` places it.

| Address / value | Name | What it is | Evidence |
|---|---|---|---|
| `0x00407f82` | | The park level load's one call of `FUN_00550e00`, on `<level>\Scape.omp` (`0x00747ee8`), before the sky's loader | Decompiled (`FUN_00407f20`) |
| `0x00550e00` | | The file's reader: `OBJ_`, a count, a record size, then each record's first fifteen words into a cleared buffer, a longer record's rest stepped over; a dispatch on word 0 | Decompiled |
| `0x00551006` | | Type 2: `Particles_Spawn( word 1, word 3, word 4, word 5 )`, the answer dropped | The listing: the four pushes read `[ESP+0x30]`, `+0x2c`, `+0x28` and word 1 |
| types 1 and 3 | | Sounds (`FUN_0051c130`; `Sound_PlayEffect` then `FUN_0051c5d0`): "The land's own sounds", below | Decompiled |

**Measured on all four parks' files** (FileFormats `omp.md` has every record): the jungle's holds seven
records, the other three four each; **the jungle's third is the only type 2 the game ships**:
(2, 20, 0, 545100, 5000, 538700), effect 20 at 532.3, 4.9, 526.1. Effect 20 is the library's `WaterFall`:
endless, rates 2, 1, 1, 1, a particle living 25 ticks and up to five more, thrown out inside an ellipse of 468
(7.3 units) each way, growing from 1500 to 5000, set 13's six frames, blended. It is the first effect the
park's system starts, which is why the shipped file holds it in slot 0 under handle `0x10000`.

**What OpenTPW does.** `PlacedObjectsFile` reads the file as the loader does; `ParkPlacedObjects.Start`, called
as the park's entities are made (`Level`, after `WorldParticles`), starts each type 2's effect at its place in
the park's particle system and keeps no handle. A park entered from a file starts it afresh rather than
reading the file's emitter, whose particles the original's loader empties in any case. The file's sounds are
read: a type 1 of the global category is started (`ParkPlacedObjects.StartSounds`, `ParkAudio.StartPlaced`), and a type 1 of the level's own category and every type 3 are counted (`SCAPE_OMP_SOUND_TYPE_1`, `SCAPE_OMP_SOUND_TYPE_3`: four in the jungle), and
a start the system refuses is counted (`PARK_PARTICLE_NOT_STARTED`).

**Run, 2026-10-10** (`q262/confirm.py`, 5 of 5 predictions on the desktop, each written first in
`PREDICTION.txt`). Lost Kingdom from `easymode.TPWI`: one log line `the land's own effect 20 started at
(532,4,526)`; five seconds on `particles` read two emitters of the world, the shop's bubbles and effect 20 at
(532,4,526) holding 24 particles, 36 of 36 drawn; the census counted the six sounds; `savepark`'s file still
held the file's own emitter in slot 0. The frames show the spray at the foot of the fall
(`waterfall-fix-left-control-right.png`). **The control** (the tip before): one emitter of the world, none of
effect 20, no spray. **The original under Proton**, loading the fix's file (`loadfile.sh` with `emitters.py`):
slot 0, handle `0x10000`, effect 20 at (532,4,526), no particle on the first poll after the load, two 0.05 s
later, and 24 to 28 from then on; its frame shows the spray at the same place
(`waterfall-opentpw-left-original-right.png`, the same side of the fall, not the same zoom).

**Not compared:** the spray's size and brightness in the two pictures (the lens, Q260, and the two views differ).
The density was then the options' (1000) where the original's is the file's 500 (OpenTPW's too since Q703); both
scale this effect's rates to one a tick, so the counts agree. **Not run in either game:** a park of another theme (the tests: none starts a
particle), a Full Simulation park with no file. Of 23 bugs put back, 23 fail a test (`q262/mutations.py`); the
call in `Level` is held by the control run alone.

### The land's own sounds

Q263, split by the session: the item's other pieces are Q264. A decode, with no code: what `scape.omp`'s type 1
and type 3 records ask of the sound engine, held against the running original's voices. The engine's side (a
voice's range, the rectangle, the distance mapping) is `audio.md`, "A voice's range, and the rectangle it
follows the listener in"; the bytes are FileFormats `omp.md`.

| Address / value | Name | What it is | Evidence |
|---|---|---|---|
| word 1 | | The category: nought is the global `cat_ambient` (`DAT_00803a20`), anything else the level's own (`DAT_00803a38`) | Decompiled (`FUN_00550e00`) |
| `0x005510ba` | | Type 1: `FUN_0051c130( 0, category, word 2, word 3 / 1024, word 4 / 1024, word 5 / 1024, word 6 * 2 / 1024 )`, each divided toward nought | Decompiled |
| `0x0051c130` | | `Sound_PlayEffect` with a seventh number: the play record's float at `+0x18`, the voice's own range, where `Sound_PlayEffect` stores nought | Decompiled, both |
| `0x00550fe5` | | Type 3: `Sound_PlayEffect` at words 3, 4, 5, then `FUN_0051c5d0( voice, word 3, word 5, word 7, word 8 )`, each over 1024. Its word 6 is not read | Decompiled |
| `0x0051c5d0` | | Posts the voice a rectangle: x, z, x + the third, z + the fourth, as whole numbers (`FUN_006b64b0`) | Decompiled |

**The six in the jungle** (`fx` on the two categories; the flags word is the effect record's `+0x10`):

| Record | Effect | Samples | Flags | What the engine makes of it |
|---|---|---|---|---|
| type 1 at (530, 513) | global ambient 8 | `watfall.mp2`, 1.8 s, volume 100 | `0x0008` | A looped voice with a range of 97: the waterfall, 13 units from its spray |
| type 1 at (511, 664) | global ambient 7 | `River.mp2`, 1.7 s, volume 50 | `0x0008` | A looped voice with a range of 97 |
| type 3 at (353, 31) | global ambient 2 | `gull2.mp2`, `gull3.mp2`, a wait of 0 to 10 s | `0x0006` | A voice kept at the listener's place inside (353, 31) to (450, -66) |
| type 3 at (372, 21) | global ambient 3 | `SEA1`, `SEA7`, `SEA8.mp2` | `0x0006` | The same inside (372, 21) to (567, -174) |
| type 1 at (21, 39) | the level's ambient 181 | one variation | `0x0206` | Range 19; no such voice stood in the original's list on any poll |
| type 1 at (19, 69) | the level's ambient 182 | no variation | `0x0008` | Nothing to play |

**Measured in the original, 2026-10-10** (`original/voices.py`, new: the service's own voice list; the shipped
park loaded from eight files whose cameras stand at stepped distances from the fall, `q263/mkfiles.py`). Predicted
before each read from the range alone, 4 of 4: the fall's voice sounding and the river's silenced with the
listener 21, 26 and 49 units from the fall; both sounding at 86 from the fall and 75 from the river; the river
alone at 31 from it and 160 from the fall; neither at 281 and 134. A voice out of range carries flag `0x800`
and no channel (`0x00886`), one in range `0x00087`, and the fall's held its channel through 28 polls in 14 s on
a 1.8 s sample. Both read a range of 97.0. The listener is the midpoint of the camera and the point it looks
at, as `scenes.md`, "Positioning" has it, and the distance is over x and z alone. The gulls' voice stood at
(450, the listener's height, 31) and the sea's at (the listener's x, its height, 21) on every poll, the
listener being past both rectangles' near edge; both were silenced there.

**How loud a voice is inside its range** is `QMixer.dll`'s law, read and measured for Q264 (`audio.md`, "The
mixer's distance law"): `((range - d) / (range - 2)) ^ 0.8`, d over all three axes. Two captures of the
original's mix had not separated the fall from it (its sample matched at 0.04 at best, the mix 0.04 to 0.07 rms
with every placed voice silenced); the mixer's own channel was read instead. The class of the gulls' and the
sea's effects (`0x0006`, `0x006be090`) is `audio.md`'s "The plain class: any variation, and a silence after a
sample".

**What OpenTPW does** (Q264, split by the session: the item's other pieces are Q265). A type 1 record of the
global category whose effect is a looped one (flag `0x8`) with something to play is started as the park's
sound comes up (`ParkPlacedObjects.StartSounds`, `ParkAudio.StartPlaced`): a looped voice at the record's place,
the height the world's third axis, with the record's range, at its variation's volume, 100 for the fall and 50
for the river. A hundred plays at the music's level times 75 over 60, the two groups' levels as the original's
channels read them (`ParkAudio.ChannelGain`). The level's own category is not loaded, so its two type 1
records are counted (`SCAPE_OMP_SOUND_TYPE_1`). The console's
`placed` prints the listener and each of the land's sounds with its distance and gain.

**The gulls and the sea** (Q302, split by the session: the item's other pieces are Q303). A type 3 record of the
global category whose effect is of the plain sample-after-sample class is started with the park's sound
(`ParkPlacedObjects.StartSounds`'s second taker, `ParkAudio.StartFollowing`) and served each frame
(`ServeFollowing`): its voice stands at the listener's place held inside the record's rectangle, at the
listener's own height (`ParkPlacedObjects.Following.Beside`), with its variation's range, 100, and plays as
`audio.md`, "The plain class" has it. One refused, and one of the level's own category, is counted
(`SCAPE_OMP_SOUND_TYPE_3`; no shipped park has either). Every park's file holds the sea, so the other three
themes start it too; only the jungle was run.

**Run, 2026-10-10** (`q263/confirm.py`, 3 of 3 predictions, written first in `PREDICTION.txt`). Lost Kingdom at
the tip, the camera on the fall: `unimplemented` reads `4x SCAPE_OMP_SOUND_TYPE_1` and `2x
SCAPE_OMP_SOUND_TYPE_3`, the jungle's four and two records, and the frame shows the fall and its spray
(`run/waterfall.png`).

**Run, 2026-10-10, Q264** (`q264/confirm.py`, predictions written first in `PREDICTION.txt`; the game's own mix
written to a file, the music's group at nought). Run 1 held 3 of 6: the distances missed (19.27 and 59.73 where
15 and 55 were predicted: the eye is lifted by the ground's height under the point, about 12 at the fall, and
the point looked at is left at nought), and the first place was measured under the advisor's opening line,
which turns the effects down. Run 2, the distances as read and a wait of 16 s, held 6 of 6:

| Camera | `placed`: the fall's distance and gain | `watfall.mp2` in the mix | Owed (0.4125 x the master's 0.5 x the gain) |
|---|---|---|---|
| `camera 530 513 30 180` | 19.27, 0.8517 | 0.1755, four steps scoring 0.94 to 0.97 | 0.1757 |
| `camera 530 513 110 180` | 59.73, 0.4730 | 0.0981, scoring 0.87 to 1.00 | 0.0976 |
| `camera 530 713 110 180` | 235.08, 0.0000; the river 96.46, 0.0159 | no step over 0.09 | nought |

The census read `2x SCAPE_OMP_SOUND_TYPE_1` and `2x SCAPE_OMP_SOUND_TYPE_3`, and the log `the land's own
ambient 8 started at (530,513), range 97, volume 100` and ambient 7's at (511,664), volume 50
(`run-fix2/place-1.png` is the fall on the first camera). The control, the tree before the change, has no
`placed`, counts type 1 four times and has no step of the fall's sample over 0.5 on the first camera
(`run-control/`).

**Run, 2026-10-11, Q302** (`q302/confirm.py`, predictions written first in `PREDICTION.txt`; the game's own mix
written to a file, the music's group at nought). Run 3 held 16 of 16 and the control, the tree before, 4 of 4:

| Camera | `placed`: the gulls | `placed`: the sea |
|---|---|---|
| `camera 400 60 110 180`, listener (400,89,47) | at (400,31,47), 57.87 off, law and gain 0.5089 | at (400,21,47), 67.87 off, 0.4097 |
| `camera 400 0 110 180`, listener (400,29,47) | at the listener's own place, 0.00 off, 1.0000 | at (400,21,47), 7.87 off, 0.9518 |
| `camera 400 400 110 180` | 397.87 off, law 0.0000 | 407.87 off, law and gain 0.0000 |

Over 75 s at the second camera the gulls' fourteen log lines each read volume 49 playing at 0.2035 and the one
pitch the voice drew (1; -1 in run 2), the first followed by no silence (that build's rule; the first draws its
silence too since the review of Q563, `audio.md`, "The plain class") and the rest by 85 to 9913 ms; the sea's
nine each read 55 playing at 0.2255 (33 at 0.1375 in run 1), no two running of one variation. In the mix every
sea sample named was found at 1.00 and a gain of 0.1073, the 0.2255 times the master's 0.5 times 0.9518 owed
(0.0654 in run 1, its 33's), and every gull, read at its pitch, at 0.91 to 0.96 and 0.0990 to 0.0997 where 0.1017
was owed. The census read `2x SCAPE_OMP_SOUND_TYPE_1` and no type 3 (`run-fix3/sea.png` is the harbour on the
first camera). The control has no start line, no following sound in `placed`, counts type 3 twice and has a mix
of exact nought at the same camera. **Run 1 held 9 of 14 and run 2 15 of 16**: the distances were predicted on
whole units (58.00 where 57.87 was read), a gull between two samples read a gain of nought and then the last
voice's 1.0000 (the census now prints `none`), a sea sample's quiet first second and a half scored 0.20 under a
gull (matched from 3 s in it scores 1.00), and my script used a name of the library's. Of 31 bugs put back
(`q302/mutations.py`), 31 fail a test; three survived the first tests, which were then made to move the
listener and to see a voice's first variation drawn.

**Not held against the original:** the same camera numbers in both games, not run (its eye at the same numbers is
ours, Q435, and since Q441 its picture is the same, Q260, Q441, so the listener can be compared there for a file entered without the Load Park list's 65 units, Q359); what was held is the law,
in its own mixer's memory. Not measured: the river in the mix (its gain was 0.0159 where it was
looked for); a pan against the original's.

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

Known divergences still open, both in lobby playback, which a park thing does not use: `MeshAnimator` and `MeshRotator` keep private clocks, and play `FirstFrame..LastFrame` (`MeshRotator` only as far as `MotionEnd`) where the engine plays **0 -> declared span** (164 clips disagree). For a park thing `PoseAsBuilt` hand-rolls the build's end.

`AnimationFile.VisibilityTrack` already matches the engine's visibility rule exactly. `AnimationFile.RotationTrack.Ease` obeys the easing table. The per-clip hide list is applied to the drawn model at every start by the start's flags, an idle channel's included (`RideAnimations.PoseChannel`, Q622, Q658).

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
