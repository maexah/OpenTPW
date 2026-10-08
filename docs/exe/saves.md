# Saves: `Config.tcf`, `gms.dat`, `.TPWS`

The byte layouts on this page are duplicated in the FileFormats clone (`formats/options-and-players.md` and
`formats/saves.md`), which is the authority for bytes.
This page is for what the executable does with them.

Where the original writes what the player changes. Three kinds of file: `save\Config.tcf` holds machine options
that belong to the installation, `save\users\<N><name>\gms.dat` holds a player profile — progress, tickets, keys and
the options that belong to a person — and `<playerdir>\<theme>\*.TPWS` holds a park. Every one is
`[dword version][fixed fields, raw little-endian]` with no tags, lengths or checksums, so a reader that is one field
out still produces a plausible-looking result. Traced read-only in Ghidra from `testme.exe`. Paths are relative to the
game's working directory, whose `save\` OpenTPW maps as `SaveFileSystem` (`<GamePath>/save/`).

## The shared pattern

One routine per structure serves both directions, a flag picking read (`FUN_005f5e20`) or write (`FUN_005f5e50`) —
the arrangement `OpenTPW.Files/Formats/Save/RecordStream.cs` copies.

The reads that look keyed — `FUN_004fae10` (4 bytes), `FUN_00510240` (8), `FUN_00424c20` (1), `FUN_004d37e0` (2) —
are **not** keyed. They are plain sequential reads; the name strings passed to them are debug labels only.

## `save\Config.tcf` — machine options

Read at boot by `WinMain_Main` `0x0045aab4` → `FUN_00424930`, which fills in defaults first, then treats version 0 as
"keep the defaults" and otherwise reads. Written by `GameOptions_Accept` `0x004237f0` when the options dialog is
accepted, and by `FUN_00424820` from `FUN_005c8650` on player save/deselect and on exit.

32 bytes. The offsets are fields of the in-memory options object; the file holds them in the order listed.

| Offset | Size | Field | What it is |
|---|---|---|---|
| — | 4 | version | 1 |
| `+0x00` | 4 | 3D card | dword |
| `+0x04` | 4 | resolution | dword |
| `+0x08` | 4 | graphics quality | dword |
| `+0x0c` | 4 | video card | dword |
| `+0x28` | 8 | movie | on byte + 3 padding, then volume dword |
| `+0x30` | 4 | audio quality | dword |

`safemode.tcf` ships alongside and is copied to `save\config.tcf` by `safemode.bat`; its values are
`1, 0, 1, 0, 0, 1, 100, 32` in the order above. The game writes **`Config.tcf`**, the batch file writes
**`config.tcf`** — the case differs, which matters off Windows.

`dialog.tcf` in the working directory is unrelated: developer debug flags, version >= 2, and never written by the game.

## `save\users\<slot><name>\` — players

The directory names *are* the slot table; there is no index file. The player's name exists nowhere else — only in the
directory name.

**Boot scan (`FUN_005c7590`)** creates `save\`, `save\users\` and `save\online\`, then reads each directory's `gms.dat`
mode byte. It accepts a directory named `<digit 1-4><name>` where the name is at least one character and does not
start with a dot. A later directory carrying the same digit overwrites the earlier one. A missing or bad `gms.dat`
falls back to defaults (mode 0); a partial read keeps whatever was read. It creates `<slotdir>\<theme>` for each theme.

**Create (`FUN_005c7f40`)** deletes any old directory in that slot, makes `<playerdir>\<theme>` per theme, copies
`data\levels\<theme>\easymode.TPWI` in if mode != 0 (Instant Action — OpenTPW's dialog id `0x70d`), and writes
`gms.dat`.

**Select (`FUN_005c83b0`)** loads `gms.dat` and applies its options.

**Save / deselect (`FUN_005c8650`)**, reached by Select New Player, and by exit only when a player is current, writes
`addrbook.dat`, `outbox.dat`, `gms.dat`, then `Config.tcf` — in that order. `gms.dat` is also rewritten at the start
of every park save, before the park file is opened ("Load Game and Save Game" below).

**Delete (`b_dellog`)** shows message box text UITEXT 399, then recursively deletes the player directory. The slot is
emptied and nothing else is written. Row 399 is not empty: it is a parameter string, parameter 4 then
"Are you sure you want to / delete this player ?", in both language folders; a reader that takes a string's first
text part alone reads it as empty, which is what OpenTPW's does (`QUEUE.md` Q143).

## `gms.dat` — the player profile

Version 12, `FUN_005aff50`. Defaults come from the progress object constructor `FUN_005aef50`.

| Offset | Size | Field | What it is |
|---|---|---|---|
| — | 4 | version | 12 |
| `+0x18`..`1b` | 4 | record ticket flags | four player-wide ticket flags |
| `+0x26`, `+0x27` | 2 | ticket flags | two more ticket flags |
| `+0x1c` | 4 | golden tickets | tickets **spent** |
| `+0x20` | 4 | golden keys | keys **given outright** |
| `+0x24` | 1 | mode | 1 = Instant Action, 0 = Full Simulation |
| `+0x25` | 1 | swear filter | default 1. Set after the read, `FUN_005afd70` loads `%s\Language\%s\swears.txt` and `alloweds.txt` through `FUN_005d3140`, and clears it when they fail to load |
| `+0x4c` | 1 | first park not started | default 1 |
| — | 4 | N | theme count, starts 0 |
| — | var | themes | N × (dword name length + name chars + theme record) |
| — | 39 | options | per-player options, `FUN_00423e90` |
| — | 4 | M | ride id count, starts 0 |
| — | 2×M | ride ids | M words: the items unlocked with golden tickets, the set the buy list asks (`hud.md`, "The mystery row") |

**Theme record** — a `0xbc` object:

| Offset | Size | Field | What it is |
|---|---|---|---|
| — | 6 | park ticket flags | six per-park ticket flags |
| — | 4×5 | records | 4 × (byte "holds record i" flag, dword record value); the value is garbage while its flag is 0 |
| — | 33×4 | park name | 33 × (word `mSignNameA`, word `mSignNameB`) — UTF-16 |
| — | 1 | `mNameChanged` | |
| — | 1 | `mAllResearchCompleted` | |

Theme records are created lazily: at select, one per theme in the list at `DAT_00786b90`. They are held in a tree
sorted by case-sensitive byte compare and written in that order. Duplicate names fail the load.

**Per-player options (`FUN_00423e90`)** — 39 bytes: 8 bytes each for SFX, music, speech and movie (on byte + pad +
volume dword), then one byte each for `AdvisorOn`, `TutorialOn`, `TooltipsOn`, `ConfirmDeleteOn`, `RMBScrollOn`,
`RMBCancelOn`, `IsometricOn`.

A new player's first `gms.dat` is **68 bytes**: 4 + 4 + 2 + 4 + 4 + 3 + 4 + 39 + 4. The 39 option bytes are whatever
`GameOptions` happens to hold at that moment.

## Keys and tickets

Tickets `T` = nonzero(`+0x18`..`1b`) + nonzero(record `+0`..`5`, counted only for themes whose `global.sam` loads) +
nonzero(`+0x26`, `+0x27`).

- `PlayerProgress_CountKeys` = `T / 3` + (`+0x20`)
- `PlayerProgress_AddKey` = `++(+0x20)`

`FrontEnd_ClosePlayerSlots` gives the first key only to a **new Full Simulation player**, then rewrites `gms.dat`
immediately, with advisor line `0x189`. An Instant Action player gets no key, hears advisor line `0x18a`, may enter any
island, and has ticket awards switched off entirely.

**What an award returns** (Q194; the checkers are `ride-operation.md`, "Golden tickets"). `FUN_005af810` (Local) returns
0 when the park holds the ticket already; otherwise it sets it, counts `T`, and returns 1 (ticket only) unless
`T / 3` + (`+0x20`) is above nought and `T % 3` is 0, when it returns 3 (ticket, key and park) if a theme's key
requirement equals the new key count and 2 (ticket and key) if not. `FUN_005afb00` (Secret) does the same on `+0x26 + i`.
`FUN_005af940` (Global, given the measured value) differs: if the player holds global flag `i` (`+0x18 + i`) and this
park does not hold the record, it looks for another of the player's parks holding it with a **smaller** stored value,
moves the record here with the new value and returns 4 ("Global ticket award moved here"; no new ticket), else 0. A
park that holds the record never has its value raised. A player without flag `i` gets it, the record on this park,
and 1, 2 or 3 as above.

## Which option lives where

| Option | Lives in |
|---|---|
| 3D card, resolution, graphics quality, video card, audio quality | `Config.tcf` only |
| SFX / music / speech on + volume; the seven switches | `gms.dat` only |
| Movie on + volume | **both** — the player's copy wins on select |

Accepting the options dialog writes only `Config.tcf`. Player options reach disk at the next player save.

## Parks: the `.TPWS` container

Written as `<playerdir>\<theme>\<name>.TPWS` for a menu save, `<theme>.TPWS` for a quicksave, and `autosave.TPWS`.
Alongside them: `restart.INTS`, `Refresh.INTS` (written around a sound-quality change) and `upload.LAYS` (layout only).

### Version is 400 **or** 500

The container opens with a dword version. **The shipped park reads 400; a saved park reads 500.** It is a version, not
a magic number — reading it as four bytes of "F4 01 00 00 magic" is what hid the distinction; the FileFormats
`saves.md`, "Header", reads it as a version. `SaveReader` accepts both and reports any other number it finds;
`ParkSaveTests.TheShippedParkIsVersion400` pins that.

### Preamble

The fixed 1549 bytes before the compressed block are laid out in the FileFormats `saves.md`, "Header", by the loader's
own reads: `FUN_00414d40` reads the version, then `FUN_00416240` a byte, the `0x500`-byte legal text (checked by
`FUN_005f7e60`), a `0x100`-byte block (`FUN_0051ab60`), the magic read big-endian against `0x01221985`
(`DAT_00749870`) and the online-header flag dword; with the flag nought, `BILZ` follows at `0x60D`, and with it set
an author header comes first (`FUN_00418da0`). All nine park files here agree with
it: the shipped `Easymode.TPWI` and the eight Alexah's Full Simulation play wrote (`CLAUDE.local.md`). The body
inflates to modules, each **followed** by a four-character marker (`WRLD`, `PART`, `CLOK`, ...).

The compression is zlib 1.1.3 ("Load Game and Save Game", "The writer").

### Loading the modules

`FUN_00415270` reads the inflated body module by module, and after each it checks the tag against the module's
`SAD_` name (the FileFormats `saves.md` table lists both names). OpenTPW reads none of the modules below.

**Particles.** `FUN_0051f680` writes the module, logging `PAR_SaveStatus failed` (`0x0074c1e4`); its one caller
(`0x0041661e`, in `FUN_004164c0`) passes 0 for the pool, so the pool is always written empty. `FUN_0051f7a0` reads it
(`0x00415437`, `PAR_SaveStatus FAILED` on a non-zero return). It reads nothing if particles are off in the running
game (`DAT_00816d40`), and the tag check then fails. A wrong magic returns 0 having read four bytes; a size other
than `0x9e68` or `0x8b60` returns 1. It keeps the running header's `+0x1c` and `+0x24` across the read, puts a new
pool of `count × 0x34` at `+0x28`, and, with the empty pool every save carries, rebuilds the pool's free list, sets
`DAT_0080ced0` and `DAT_00816d24` to 0 and clears every live emitter's particle chain (`+0x08` 0, `+0x06` -1). Then `Particles_KillAllOnScreen` (`0x005200b0`, its only caller `0x0041545a`) kills every
live emitter drawn on the screen (`+0x05` set: `+0x20` = -2, `+0x2a` = 1) and every effector acting on screen effects
(`+0x45` set: `+0x4c` = -2). After `Easymode.TPWI` that leaves `WaterFall` (emitter slot 0) and `Bubbles` (emitter slot 20); its two
`Button` emitters die. The `.emt` effects go in by `FUN_0051fa20` (called at `0x00414552`), into the first template
slot with an empty name, with no size check; only `Particles_LoadPlb` (`0x0051f370`) turns a 0 at `0xA2` into 1000.

**Game system.** `FUN_005506e0` reads nine dwords, adding the clock reading `DAT_008786bc` to the last three; `FUN_00550520` writes them (`0x00416822`). Dword 5 comes from a
stack slot the writer never stores (`LEA EAX,[ESP+0x14]` at `0x00550627`), so it is whatever was on the stack: 2 in
every played file, a pointer into a string in `Easymode.TPWI`. The reader drops it.

**Cheats.** `FUN_00405570` reads two bytes into the cheat object at `0x00fb3520`, to `+0x0a` and `+0x0b`; `FUN_004055d0`
writes them (`0x00416c98`). `+0x0a` is the cheats flag (getter `FUN_00405560`): `Park_MouseMessageProc` checks it
(`0x004888fd`, `0x0048898c`) before it runs the cheat table at `DAT_0078733c`, and `FUN_004053c0( 1 )` sets it in
play (from `FUN_004993e0`, `0x0049960a`, after two compares with obfuscated strings). `+0x0b` is touched by nothing
else but the constructor `FUN_004053b0`; its meaning is unknown. `Easymode.TPWI` ships the flag set.

**Action ids.** The action recorder's ids, and the names `FUN_004041d0` gives them, are `park-engine.md`, "There is no
drag".

## Load Game and Save Game

The park menu's rows 1 and 2 (`FUN_0048b6a0`, `scenes.md`). Decoded in Ghidra and run in the original under Proton
on 2026-10-08 (Q241); what was only read is listed at the end. OpenTPW builds none of it: both rows are counted
(`LOAD_GAME`, `SAVE_GAME`) and close the menu.

### The two rows

Each row first does what Resume Game does: with `g_ParkRunning` (`0x00786ba4`) nought and the flag at `0x007c2518`
clear, it sets the park running and resumes the clock (`FUN_00409300`), then hides and destroys the menu list
(`MenuList_Hide`, `MenuList_Destroy`, `0x007c2534`). Then Load calls `FUN_0049efb0( 0 )` (`0x0048b731`) and Save
`FUN_0049f280( 0 )` (`0x0048b809`). Row `0x16` is a second Load that hands `FUN_0049efb0` a 1; the function reads
no argument, so the two are alike.

### One screen, two uses

Both openers pause the game as a message box does (`FUN_004092a0( 0, 0 )`: the clock held, the advisor's voice
paused), set `g_ParkRunning` to nought, close the park screen that is open (`FUN_00485b40`), and load the stream at
`0x007523f0` with `UI_LoadModalTree`, so the screen is modal and has the focus. Load's handler is `FUN_0049e880` and
its window is kept at `0x007cb244`; Save's handler is the code at `0x0049ea30` and its window at `0x007cb248`.

The stream, walked clean by `treewalk.py` (rects in the 2048 by 1536 space):

| Control | Type, flags | Id | Rect | Mesh (`ui.wad` node) |
|---|---|---|---|---|
| the frame | 1, `0x1` | `0x23bcdf` | 248, 30, 1800, 1007 | `window2` (`w_med.MD2`) |
| cancel | 2, `0x1` | -2 | 1671, 818, 1754, 901 | `b_exit` |
| OK | 2, `0x1` | -1 | 1660, 730, 1743, 813 | `b_okay` |
| the list | 7, `0x81` | `0x23bce0` | 341, 203, 1614, 772 | `f_load`; rows in 371, 229, 1511, 733 |
| its scrollbar | 3 | 1 | 1528, 285, 1587, 691 | skin `!slider`; `b_up`, `b_scroller`, `b_down` |
| the title | 1, `0x1` | `0x23bce1` | 746, 83, 1218, 162 | none |
| the name box | 5, `0x1` | `0x23bce2` | 605, 811, 1343, 895 | `!frame`; text in 638, 830, 1317, 874 |

The list has two columns, 371 to 1015 and 1041 to 1511, both typed text (`FUN_006636b2( 0, 0 )`, `( 1, 0 )`), and
no headings: the stream has no `op 0xc`, and UITEXT 203 "Filename" and 204 "Date" are read by neither opener. Its
flags carry `0x80`, so a row is selected under the moving pointer and `0x400` is posted on a click (`hud.md`, "The
pointer over a list"). The row widgets are font 7 (`FUN_00485a70( 7 )`), white, the second column's text set by
`FUN_0065c428( 2, 1 )`; the row's height is the font's height times `0x600` over `[0x00faa5c0]`, plus 6
(`FUN_0049f0b0`, kept at `0x007cb24c`); the selection is the `hilight` skin.

**Load** titles it UITEXT 202, "Load Park", and hides the OK button and the name box (`UI_SetVisible( 0 )`,
`0x0049f02a`, `0x0049f043`). **Save** titles it UITEXT 201, "Save Park", and sets the name box up: font 7, the
buffer `0x007ca720` with room for 15 characters (`FUN_006662b7`), the `hilight` skin for its selection, the text
UITEXT 206, "New Save" (the box's vtable `+0x24`), and the characters it refuses, `\ / * ? : | < > "` (the wide
string at `0x00752588`, stored at the box's `+0x144`). Then it turns the keyboard shortcut tables off
(`FUN_00486b70`: `FUN_0040cfa0` and `[0x007c24d0]` 1), so a typed letter is a letter; closing turns them on again
(`FUN_00486b60`).

### The list is the folder

`FUN_005ac8f0` (on the save manager at `0x00f7b560`) empties the manager's list and reads the player's folder for
the theme afresh every time it is called: `FUN_005c8890` gives `<player's folder>\<theme>`, and the pattern is
`*` with `.TPWS` (`0x00f7b948`, `0x00f7b548`). Each entry found is kept unless it is a folder whose name does not
begin with a dot (`FUN_005c5230`); its name less the extension's five characters, and its last write time (the find
record's `+8`, which `FUN_00619bc0` takes from `WIN32_FIND_DATA.ftLastWriteTime`), go on the end of the list. So
the rows stand in the order Windows hands the files over, `autosave.TPWS` and the quicksave `<theme>.TPWS` among
them; `easymode.TPWI` and `restart.INTS` do not match. `FUN_005accb0( entry )` is the entry after one.

Both fills (`FUN_0049ec80` for Load, `FUN_0049edf0` for Save) add one row an entry, its id the entry's place in the
list: the name, then the time as local time (`FileTimeToLocalFileTime`) written by UITEXT 448 with the day as
parameter 16, the month as 17 and the year as 18, then a space and `%02.2d:%02.2d` of the hour and the minute
(`0x00752564`). Row 448 is `{16}.{17}.{18}` in the English folder and `{17}.{16}.{18}` in the american one, the
numbers not padded: the original, which reads the american tables here, showed "10.8.2026 12:28" for a file
written on 8 October.

### What the handlers answer

| Message | Load (`FUN_0049e880`) | Save (`0x0049ea30`) |
|---|---|---|
| `0x100`, a button | -2: close | -2: close. -1: save (below) |
| `0x400`, a click on a row | load that entry (`FUN_005ac5d0`), then close | put that entry's name in the box (its vtable `+0x24`) |
| `0x802`, `0x804`, Enter and Escape in the name box (`lobby.md`) | | posts `0x100` with -1, with -2 |
| 5, "close yourself" (`FUN_00485b40` sends it) | close, answer 1 | close, answer 1 |
| `0x14`, the window going | window pointer nought, `FUN_004862a0`, `g_ParkRunning` 1, `FUN_00409300` | the same, after `FUN_00486b60` |

"Close" is `FUN_00658d9f( window, 4, 0, 0 )`. Each walks to the entry by counting `FUN_005accb0` from a fresh
`FUN_005ac8f0`, so a click re-reads the folder.

**Load asks nothing.** The click loads over the running park at once.

**Save's OK** re-reads the folder and compares the box's text with each entry's name (`FUN_0067c290`). With no
match it saves (`FUN_0049e9b0`). With one it opens a message box, UITEXT 205 handed the name as parameter 2 -
"New Save exists / Overwrite ?" - whose yes is `FUN_0049e9b0`. `FUN_0049e9b0` makes a string of the buffer, calls
`FUN_005ac610` on the save manager and closes the screen; it does not look at what the save answered.

### The save

`FUN_005ac610( name )` is `FUN_005ac780( name, L".TPWS" )` (`0x00f7ad98`); `FUN_005ac630` is the same with
`L".INTS"` (`0x00f7adf8`), and `FUN_005ac5d0` / `FUN_005ac5f0` are the two loads through `FUN_005ac650`.

1. **The player first.** `FUN_005c8a10` writes the current player's `gms.dat` (`FUN_005c8a20`, `FUN_005afc60`),
   before the park file is opened. Under Proton `gms.dat` was written 28 ms before the park file, its bytes unchanged.
2. **The path** is `FUN_005c8890`'s folder, `\` (`0x00f7b888`), the name, the extension.
3. **`FUN_00414920( path, 0, 2 )`** on the object at `0x0078a450`, whose `+4` takes a failure's number: 1 the file
   would not open, 2 no memory, 3 a write failed, 5 a module failed. A failure deletes the file (`FUN_005f5d60`).
   Nothing was found that shows the number to the player.

`FUN_00414920`'s third argument is the mode, which must not be nought; 1 writes the action recording alone (the
Publish Park upload at `0x0048b4b0`, which also hands it an author header), 2 everything. Its second is the
author header, nought for none.

### The writer

- The version, 500 (`0x006fd928`).
- **`FUN_00415f50`, the preamble**: one byte for the running language (English 0, Spanish 1, Italian 2, Swedish 3,
  German 4, French 5, Japanese 6, anything else 0); `0x500` bytes of that language's legal text from the table at
  `0x0078a460` (stride `0x14`: the length at `+4`, the text at `+8`), zero-filled; `0x100` bytes of zeros with 32
  bytes from `0x00802080` at their head; the magic `0x01221985` through `htonl`; the flag dword, nought here. With
  the flag set the author header follows (`FUN_00418da0`).
- The file is closed. **The body** is written to a memory file (`FUN_005f72c0`, `0x200` to begin with) by
  `FUN_004164c0`, after `FUN_00402e60` stamps two clock readings into the saver.
- **The body is deflated** (`FUN_005f8050`, the compressor's vtable `0x00703128`, slot 1, the code at
  `0x00619280`): `deflateInit2_( strm, -1, 8, 15, 9, 0, "1.1.3", 0x38 )` and one `deflate( Z_FINISH )` into a
  buffer as large as the body, behind a 28-byte header: `BILZ`, the body's length, the stream's length plus 28,
  then 15, 9, 0, 0. A stream that would not fit asserts "Save game is too small to be saved".
- The file is opened again and the block written at its end.

**`FUN_004164c0` writes the modules in this order, each followed by its tag:** the action recording
(`FUN_00403780`, no tag); World `FUN_00516c80` `WRLD`; sprite scripts `FUN_00475650` `SPSC`; particles
`FUN_0051f680` `PART`; the message centre `FUN_0040fc80` `MESS`; the clock `FUN_00402e00` `CLOK`; vanilla time
`FUN_00403220` `VANT`; the game system `FUN_00550520` `GSYS`; the ride system `FUN_00464140` `RSYS`; track rides
`FUN_005428e0` `TRAK`; flying rides `FUN_0055de70` `FLYR`; ride scripts `FUN_00559350` `RSSE`; the camera
`FUN_0042cdc0` `KAME`; coasters `FUN_00437300` `COAS`; the advisor `FUN_00599c30` `ADVS`; sound `FUN_0051c350`
`SOUN`; cheats `FUN_004055d0` `CHTS`; advisor scoring `FUN_0059c890` `ADSC`; and the UI block `FUN_004816a0`, no
tag. That is the order the files hold (FileFormats `saves.md`, "Inside the stream"). In mode 1 only the action
recording is written.

**Measured.** In all ten park files here (the shipped park, the eight of Alexah's Full Simulation play, and one
Instant Action save the original wrote under Proton on 2026-10-08) the stream opens `78 9C`, the header's last four
dwords are 15, 9, 0, 0, and deflating the inflated body again at level 6, window bits 15, memory level 9 gives the
stored stream back byte for byte; at memory level 8 it does not in any (`measure.py`, predicted first). .NET's
`ZLibStream` has no memory level to set, so a file OpenTPW writes with it will inflate the same and differ in its
compressed bytes.

### The load

`FUN_005ac650` builds the same path and calls `FUN_00414d40( path, 0, 2 )`: the version (above 500 is refused in
mode 2, failure 9), the preamble (`FUN_00416240`, failure 7), the rest of the file read whole and inflated
(`FUN_005f8120` for the size, `FUN_005f8100`; failures 4 and 6). Then, in any mode but 1, **the running park is
taken down where it stands** - `FUN_00457e10`, `FUN_004d8330`, `FUN_0052f8d0( 0x80, 0x80 )`, `FUN_005191e0`,
`FUN_00515fb0`, `FUN_00515f30`, `FUN_0042a190`, `FUN_005445d0`, `FUN_00437150`, `FUN_005584b0`,
`Advisor_StopQuietly( 0 )`, `FUN_004815d0` - the modules are read over it (`FUN_00415270`, "Loading the modules"
above), and `FUN_00415140` puts it back together (its calls run from `FUN_005508c0` to `FUN_0051c1c0( 0 )`, with
the loaded fonts dropped on the way; none is decoded here). A module that fails leaves "Loading failed. System state now undefined" in the log and
failure 6. There is no loading screen and no change of scene: under Proton the park was back four seconds after
the click, its date gone from 2.9.2000 to the save's 2.7.2000.

### The other callers, not this item's

| Site | What |
|---|---|
| `FUN_0040bf90`, `FUN_0040c040` (reached through a table, no direct caller) | Quicksave and quickload: the name is the theme's (`FUN_005c5520`), so `<theme>.TPWS` |
| `0x0054ff10` in `Game_StateMachine`; `0x00424bd7` | `FUN_005ac610( L"autosave" )` |
| `0x00550c23`; `0x0055036c` | `restart.INTS` written when the folder has none; read by Restart Park |
| `0x00407eae` in `FUN_00407e00` | a `.TPWS` loaded by name as the world is made |
| `FUN_005accf0`, `0x0054f12b` | entering a park: the newest `*.TPW*` in the folder (`park.md`, "Arrivals") |

OpenTPW's `Level` reads `data/levels/<theme>/Easymode.TPWI` on every entry, not the player's folder.

### Read, not run

A typed name, Enter and Escape in the box, a row's click on the save screen, a refused character, the sixteenth
character, a failed save or load, and a folder with more than one save (the rows' order) were not run in the
original: the listing's alone. The `0x100`-byte field's 32 bytes at `0x00802080` and the failure number's reader
are not traced. `FUN_00415140` and the twelve teardown calls are named, not decoded. `addresses.md` is not
regenerated.

**The harness** is `q241/` in the harness folder: `ghidra/` (the dumps), `treewalk.py <address>` (any layout
stream in the executable), `strraw.py <file.str> <row>...` (a row's parts, parameters shown as `{n}`),
`measure.py`, `PREDICTION.txt`, and `orig/` (`lib.sh`, the frames `s1` to `s5`, `l1`, `l2`,
`PREDICTION-result.txt`, and `New-Save-written-by-the-original.TPWS`, an Instant Action Lost Kingdom save).

## What OpenTPW builds

| Piece | File |
|---|---|
| One dual-direction record routine per structure, as the original has | `OpenTPW.Files/Formats/Save/RecordStream.cs` |
| `Config.tcf` | `OpenTPW.Files/Formats/Save/ConfigFile.cs` |
| `gms.dat`, plus `ParkRecord` and `PlayerOptions` | `OpenTPW.Files/Formats/Save/PlayerFile.cs` |
| Paths, case-insensitive finds, scan / create / save / delete | `OpenTPW/Client/SaveFolder.cs` |
| Atomic writes (write a `.tmp`, then move it over) | `OpenTPW.Common/Files/BaseFileSystem.cs`, `WriteAllBytes` |
| Player slots, persisted | `OpenTPW/Client/Players.cs` |
| `.TPWS` / `.TPWI` container | `OpenTPW.Files/Formats/Save/SaveReader.cs` |
| The inflated body: the World block, and what each thing and script was doing | `OpenTPW.Files/Formats/Save/ParkWorld.cs`, `ParkThingStates.cs`, `ParkScriptStates.cs` |

Themes are taken to be the `data/levels` folders containing a `global.sam`. This is **inferred** — the original's own
theme list names could not be read out of the executable.

Confirmed in the running game: `safemode.tcf` round-trips byte for byte; create → quit → relaunch gives the welcome
back screen with key 1 and per-player options restored; delete works; an Instant Action player gets no key, response
394 (sample 469), and `easymode.TPWI` copied in.

The original's own `Config.tcf` and `gms.dat` exist, in Alexah's Full Simulation saves (`CLAUDE.local.md`).
A disposable copy of the Full Simulation profile reads complete: four parks, one unlocked ride and one key;
an award persists as two keys. A truncated copy remains unchanged when a default profile is offered for saving.
The original machine options have not been checked here.

Caution: a run of the game writes into the real installation's `save/` (`Config.tcf`, `opentpw.cfg`, player folders).
Never empty it: delete only what that run created (`CLAUDE.md` rule 12).

### OpenTPW preservation policy

OpenTPW may display the readable prefix of an incomplete player, but refuses to write it back. `PlayerFile.CanWrite`
requires a complete version-12 record with no trailing bytes; machine options require version 1 with no trailing
bytes. `SaveFolder` also rechecks an existing destination before replacing it, so a failed load followed by default
in-memory state cannot overwrite unread data. This is a deliberate preservation policy, not recovered executable
behaviour. A player loaded read-only can still be selected, but their changes remain in memory and the log says why.
`ProfilePreservationTests` drives selection, key awards and deselection over disposable files, including truncation,
unknown versions, invalid counts, trailing bytes and a destination damaged after selection.

**A reload that fails, and a file that is not there (Q207, Q209).** When a player is selected and their gms.dat is
there but will not read, the cached profile keeps its readable progress and loses `CanWrite`; key saves and
deselection stay blocked even if a valid file returns, and only a successful reload restores writing. A folder with
no gms.dat at all is different: it is a player nobody has saved yet (`SaveFolder.ScanPlayers`), there is nothing to
protect, and the first save makes the file, as the original's writer does (`0x005afc60` writes unconditionally).
`Player.FileMissing` records that no file was there when the player was read; if one has appeared by the time of
the save it was never read, and `SaveFolder.SavePlayer` leaves it alone. None of this is the original's behaviour.

`ProfilePreservationTests` covers each: a folder with no file is saved and reads back; a file appearing after such a
read is not replaced; a file that would not open is not replaced once it can; a file removed and then restored with
other contents during a session is kept, and a successful reload then saves normally.

## Unresolved

- What "tcf" stands for.
- The theme list's own names.
