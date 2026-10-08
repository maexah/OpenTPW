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

**Camera.** `FUN_0042cec0` reads the zoom, the rotation, the flags, the point looked at, a saved point and a saved
rotation into `0x007909ec`, `0x00790a38`, `0x00790ab0`, `0x007908f0`, `0x00790ad0` and `0x00790a9c`
(`FUN_0042cdc0` writes them). Then it zeroes the flags (`0x0042d04c`), copies the rotation
to `0x007909e0`, and, when the options byte `0x0078d912` is set, **rounds the rotation to a quarter turn**
(`0x0042d05e`): `FUN_0067b24a` of the rotation and the double π/2 at `0x006fdd88`, taken to be the remainder
(not read), is taken off, and the rotation moved on to the next quarter when that is past an eighth
(`0x006fdd98`, `0x006fdd90`). The rotation is in radians: with the byte set (`0x0042b471`) the camera's update
turns it a quarter turn a press (`0x0042b489`, `0x0042b4a4`, the constants `0x006fdd94` and `0x006fdd68`). Which
option the byte is was not traced (`OptionsScreen_ControlChanged` writes it, `0x004a37f9`); the options screen's
90-degree rotation is the likely one (Q23). Measured under Proton (Q241f): a file holding -0.7854 read 0.0 after the load, and one holding
±1.5708 read as written.

**Action ids.** The action recorder's ids, and the names `FUN_004041d0` gives them, are `park-engine.md`, "There is no
drag".

## Load Game and Save Game

The park menu's rows 1 and 2 (`FUN_0048b6a0`, `scenes.md`). Decoded in Ghidra and run in the original under Proton
on 2026-10-08 (Q241, Q241d); what was only read is listed at the end. OpenTPW builds the Load Park screen and the
Save Park screen ("OpenTPW's Load Park" and "OpenTPW's Save Park", below); the save itself is counted
(`SAVE_GAME_WRITER`), what it must write is "What a park file must hold to be written", and the first stage of the
writer, reached from the console alone, is "OpenTPW's writer, the first stage".

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

**The name box.** A character typed with nothing selected is inserted only while the text is shorter than the
room `FUN_006662b7` gave (`0x006679dc`, the box's `+0x13c`, 15), and not at all when it is in the refused list
(`0x006678ca`, `+0x144`); Enter, Tab, Backspace and Escape are not characters (`0x0066786a`). The box's setter
(`FUN_00666308`, vtable `+0x24`) copies at most that many characters and ends in `FUN_006668e9( -5, 0, -1 )`,
where the select-all `FUN_006677ae` ends in `( -5, 1, -1 )`: text put in by the setter is not selected, and the
caret stands at its end. The opening's "New Save" is selected because the opener gives the box the focus after
setting it (`FUN_0065e59b`, `0x0049f3a6`).

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

Enter and Escape reach the handler as `0x802` and `0x804` and it posts itself the button's message
(`FUN_00658ccc`, `0x0049ec4d`), which `UI_MessageHook_ClickSound` clicks for like any `0x100`.

"Close" is `FUN_00658d9f( window, 4, 0, 0 )`. Each walks to the entry by counting `FUN_005accb0` from a fresh
`FUN_005ac8f0`, so a click re-reads the folder.

**Load asks nothing.** The click loads over the running park at once.

**Save's OK** re-reads the folder and compares the box's text with each entry's name (`FUN_0067c290`, the 16-bit
characters one for one, so case counts). Nothing else is asked of the name: an empty one is saved as `.TPWS`, and
trailing spaces are kept. With no
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

### OpenTPW's Load Park

`ParkLoadScreen` is the stream's frame, title, list and cancel button, opened by the menu's first row, modal,
pausing, closing the park screen that is open. The title is lettered as `UI_SetTitle` letters one (`0x00485d20`):
font 5 in (234, 239, 102), in the stream's rectangle widened by half its width either side. The rows are
`SaveFolder.SavedParks`, the folder's `*.TPWS` read afresh at the opening and again at the click, in name order
where the original's are in the order Windows lists them. A row's date is `Localization.Format( 448, ... )`, which
fills a string's parameter parts by number (`StringFile.Parts`; FileFormats `strings.md`, "The note on parts");
OpenTPW reads the English tables, so a file of 8 October reads "8.10.2026 12:28".

**The row's height** is the factory's sum: font 7's line height (the `.bf4` header's byte 5) times `0x600` over the
height of the screen its font set is drawn for, plus 6. The original at 640 by 480 keeps 41 at `0x007cb24c`
(`GAME8.bf4`, 11), read from its memory.

**Two deviations, said at the site.** A row's click ends the park's scene and builds it again from the file behind
the loading screen (`Game.RequestParkLoad( theme, file )`, `Level.CreatePark`'s `parkFile`), where the original
reads the file over the running park. And the list's scrollbar is not built: the wheel scrolls it, and a list
longer than its window is counted (`LOAD_PARK_LIST_SCROLLBAR`). Row `0x16`, the second Load, is reached by
nothing here.

**Measured (Q241c).** In OpenTPW, a private game folder whose player holds `New Save.TPWS` (`mGameTick` 840,
balance 88112) and `Old Park.TPWS` (a copy of `easymode.TPWI`: 755, 87987): the list read "New Save 8.10.2026
12:28" and "Old Park 1.10.2026 14:18", the clock stood at 859 while the screen was open, the cross closed it with
nothing loaded, the second row's click started the clock at 755 with 87987 in the bank and the first row's at 840
with 88112, each predicted first (`q241c/run1`, seven of seven). In the original, the same two files in the
reference player's folder: the same two rows in the same order, dated 10.8.2026 and 10.1.2026 by its american
tables, 21 px apart on a 768-line picture of its 480; the clock held at 955 under the screen and read 755 inside
1.3 s of the second row's click, the world pointer unchanged (`q241c/orig/l1.png`, `a.log`; three predictions of
four held, the row pitch wrong because the screen height was taken as 768).

### OpenTPW's Save Park

`ParkSaveScreen` is the same frame, title, list and cancel button (`ParkFileScreen`, which `ParkLoadScreen` shares)
with the stream's OK button and name box, opened by the menu's second row. The box is a `UiEdit` of fifteen
characters refusing the nine, opening on UITEXT 206 selected and holding the focus, which is what keeps the
park's shortcuts from hearing the keys (`WindowStack.Keyboard`). A row's click puts the entry's name in the box
unselected; OK, and Enter, compare the box with the folder as it stands then, by ordinal, and ask UITEXT 205
through `Localization.Format( 205, (2, name) )` in a `MessageBox` when the name is there; Escape and the cross
close. Opening the question takes the focus from the box and nothing gives it back but a click on the box, as
measured below.

**The save is not built.** Where `FUN_0049e9b0` calls `FUN_005ac610`, `ParkSaveScreen.Save` counts
`SAVE_GAME_WRITER`, logs the name and closes the screen; nothing is written ("What a park file must hold to be
written", below; Q241f on).

**Measured (Q241d).** In the original, the reference player's folder holding one save, "New Save", the box read
from its buffer `0x007ca720` (`q241d/orig`, nine predictions of nine):

- opened, the buffer reads "New Save", shown selected; "ab" typed leaves "ab";
- `\ / : < > | * ? "` each change nothing (a shifted "!" was taken, so shifted keys reach it);
- fifteen characters and no more ("aba!cdefghijklm"; four more letters and a space each dropped); Backspace
  takes one off the end; a space and a capital are taken where there is room;
- Escape closes the screen, the clock runs again and no file is written;
- a row's click puts "New Save" in the box, not selected, the caret at its end: "x" then reads "New Savex";
- Enter over "New Save" asks "New Save exists / Overwrite ?", and its cross leaves the screen and the file;
- **after that cross the box no longer has the keys**: no caret, a letter changes nothing, Enter and Escape do
  nothing (both come from the box), and no menu opens; a click on the box gives them back, the caret at the end;
- "new save" and Enter asks nothing and writes at once (under Wine onto `New Save.TPWS`, its folder being
  blind to case); "zz" and Enter writes `zz.TPWS`; the box emptied and Enter writes `.TPWS`, which the list then
  shows as a row with no name. `gms.dat` was written 28 ms before each.

In OpenTPW, a private game folder whose player holds "New Save" and "Old Park" (`q241d/run1`, nine of nine):
the screen opened on both rows with the box holding "New Save" and the keys, the clock held under it; "ab", then
the slash and backslash dropped, then "abc" with no camcorder entered, then fifteen; Escape closed it with
nothing counted and no menu; the second row's click read "Old Park", "x" made it "Old Parkx"; Enter asked "Old
Park exists / Overwrite ?"; after its cross the box was without the keys and "y" and Escape did nothing until
the box was clicked, then "Old Parky"; Enter closed the screen with `1x SAVE_GAME_WRITER` and the folder
unchanged; OK over "New Save" and the question's tick closed both, `2x`. The unchanged build's Save row opened
nothing and counted `SAVE_GAME` (`q241d/control`).

### The other callers, not this item's

| Site | What |
|---|---|
| `FUN_0040bf90`, `FUN_0040c040` (reached through a table, no direct caller) | Quicksave and quickload: the name is the theme's (`FUN_005c5520`), so `<theme>.TPWS` |
| `0x0054ff10` in `Game_StateMachine`; `0x00424bd7` | `FUN_005ac610( L"autosave" )` |
| `0x00550c23`; `0x0055036c` | `restart.INTS` written when the folder has none; read by Restart Park |
| `0x00407eae` in `FUN_00407e00` | a `.TPWS` loaded by name as the world is made |
| `FUN_005accf0`, `0x0054f12b` | entering a park: the newest `*.TPW*` in the folder ("Entering a park", below) |

### Entering a park

`FUN_005accf0` (on the save manager `0x00f7b560`, called at `0x0054f12b` in state 9 for every game type but 1, the
online one) runs after the new world is made, and loads a park file over it:

1. **The folder** is `FUN_005c8890`'s, `<player's folder>\<theme>`, and **the pattern** `*` (`0x00f7b948`) with
   `.TPW*` (`0x00f7b398`, set from `0x007479f8`). So the copied `easymode.TPWI` and every `.TPWS` are candidates,
   the quicksave and `autosave.TPWS` among them; `restart.INTS` is not.
2. Each entry found is passed over if it is a folder whose name does not begin with a dot (`FUN_005c5230`), as in
   the list's read.
3. **The first candidate is held; a later one replaces it only when its last write time is later**
   (`FUN_005f5bc0`, `CompareFileTime` of the find record's `+8`, greater than nought, `0x005acf3a`). Two files
   written at the same instant leave the one Windows listed first.
4. With one held, the path is the folder, `\` (`0x00f7aed0`), the name, and `FUN_00414d40( path, 0, 2 )`
   (`0x005ad054`) loads it as "The load" above does. **With none held nothing is loaded** (`0x005ad00b`), and the
   world just made stands: the fresh park of `park.md`, "A fresh Full Simulation park", whatever the game type.

Nothing asks which mode the player is in: a Full Simulation player's saved park is loaded the same way, and an
Instant Action player whose folder has lost its `easymode.TPWI` gets the empty world.

**Measured in the original (Q241b).** With `New Save.TPWS` (its `mGameTick` 840, written 8 October) put beside
`easymode.TPWI` (755, 1 October) in the reference player's jungle folder, and Lost Kingdom entered from the lobby,
`mGameTick` read 0 in the new world, 840 ten seconds later and one more a sweep from there, never 755; the frame
showed $ 88112, the save's balance (`q241b/orig/a.log`, `l-entered.png`; predicted first). An empty folder and two
files of one time were not run there.

**OpenTPW** does the same for whoever is playing (`SaveFolder.NewestPark`, `Level.CreatePark`), taking the
candidates in name order, so a tie goes to the first by name. With nobody playing, which only the debug console's
`park` reaches, it reads `data/levels/<theme>/Easymode.TPWI`; the original has a player on every entry.

### Read, not run

A failed save or load was not run in the original: the listing's alone. Nor was the box's selection after the
overwrite question when the name in it is the opening's own, still selected (here it stays selected once the box
is clicked), nor a click heard for Enter and Escape (the reference runs silent). A folder with two saves
was (Q241c), under Wine, whose listing order need not be Windows'. The `0x100`-byte field's 32 bytes at `0x00802080` and the failure number's reader
are not traced. `FUN_00415140` and the twelve teardown calls are named, not decoded. `addresses.md` is not
regenerated.

**The harness** is `q241/` in the harness folder: `ghidra/` (the dumps), `treewalk.py <address>` (any layout
stream in the executable), `strraw.py <file.str> <row>...` (a row's parts, parameters shown as `{n}`),
`measure.py`, `PREDICTION.txt`, and `orig/` (`lib.sh`, the frames `s1` to `s5`, `l1`, `l2`,
`PREDICTION-result.txt`, and `New-Save-written-by-the-original.TPWS`, an Instant Action Lost Kingdom save).
Q241d's is `q241d/`: `orig/` (`lib.sh`, whose `box` reads the name box's buffer, the predictions and the
frames), `confirm.py`, `mutate.py` and the sheet of OpenTPW's screen beside the original's.

## What a park file must hold to be written

Decoded and measured on 2026-10-08 (Q241e). `ParkSaveScreen.Save` still counts `SAVE_GAME_WRITER`; the first of
the writer's five stages is built ("OpenTPW's writer, the first stage", below). The bytes are the FileFormats
`saves.md`'s; this section is what the original does with them and what a writer here has to get right.

### The container takes another deflate, and a changed body

Measured in the original under Proton (`q241e/orig`, five predictions of five). The original's own Instant Action
save (`mGameTick` 840, cash 88112) was put back through four writers and each result loaded from the Load Park
screen over a running park:

| File | What was changed | The original |
|---|---|---|
| Round Trip | the body deflated again by .NET's `ZLibStream`: 39,239 bytes of stream where the original's is 37,210, behind a fresh `BILZ` header, the preamble copied | listed and loaded: 840 at the click and counting, the world pointer unchanged |
| Tick 5000 | the same, with `mGameTick` set to 5000 and nothing else | 5000 at the click, 5100 thirty seconds on; the gadget's date 8.11.2000 |
| Handles | every non-zero handle of the sprite table set to 1, the script module's saved list pointer to `0xDEADBEEF`, the game system's dword 5 to 0 | 840 and counting, the people drawn and walking 40 s on |
| Cash | the economy thing's balance set to 12345 | $ 12345 on the first frame |

So the loader does not need the stream the original would have written, only one that inflates to the length the
header gives; the three values that are addresses or stack contents of the saving session are read and not used;
and a field of the World module can be changed alone. The date follows `mGameTick` (Q149): nothing else of the
file was changed for 8.11.2000. The clock module was left as saved under a tick 4,160 later, and the park ran.

### The World writer

`FUN_00516c80( stream )`, on the world:

1. **The pointer mode is put back to the default first**: a new mode object of vtable `0x006fea10` is installed
   through `FUN_0046c350`, which calls the vtable `+0x2c` of the mode in force and deletes it. A saved park never
   holds a tool or a thing in the hand.
2. `version`, the dword 2, then the other 25 header fields in the file's order, each from its own place in the
   world (`+0x1da708` to `+0x1da746`; the FileFormats table). Logged as "World vars".
3. `FUN_004d3aa0`: the 150 object controls, their count and the search key (`mControlManager`).
4. `FUN_004d7a70`, which is three calls: `FUN_00507850` the staff pool, `FUN_004f7f30` the park clock's fields
   and `FUN_004cf050` the arrival block (`mMacroAI`).
5. `FUN_004d7ea0`: the 16,384 cells (`mMap`).
6. **The thing list.** "Used Thing Head" is the id in the head node of the used list (`DAT_007cf56c`; a node is
   five dwords: the thing, its id, the next node, the previous, one more). Then, node by node along the list: the
   **next** node's id, or nought at the end; the thing's model, the byte at thing `+2`; and the model's own
   serialiser. That is the list the sweep walks, newest first (`ride-operation.md`, "Where a ride's turn comes
   from"), so a file's order is the park's turn order.

**Every serialiser is one function for both directions**: `( stream, direction, 1, version )`, direction 1 here and
0 from the reader `FUN_005179c0`. So a record's write layout is its read layout by construction, and what the
FileFormats page takes from the readers holds for the writer.

| Model | Serialiser | |
|---|---|---|
| 1 | `FUN_004fb530` | a guest |
| 3 | `FUN_004db7d0` | a catalogue object |
| 4, 5, 6, 7, 8 | `FUN_004da110`, `FUN_004d6d60`, `FUN_004d4460`, `FUN_004d6000`, `FUN_00502760` | mechanic, handyman, entertainer, guard, researcher |
| 9 | `FUN_00508bb0` | the staff HQ |
| 10 | `FUN_004da960` | the map base (`FUN_0050b090`) and `mNextObject` |
| 11 | `FUN_00599f60` | thing 3 |
| 12, 17 | `FUN_00509bc0` | the map base alone |
| 13, 14, 15, 16, 19 | `FUN_004c5d70`, `FUN_00502dc0`, `FUN_00512010`, `FUN_004cf920`, `FUN_004d2320` | the analyser, the research lab, the weather, the economy thing, the challenge manager |
| 18 | none | refused: "Should not be able to save online persons" |

**The reader makes every thing again.** For each record it allocates the model's object, runs its constructor and
then the serialiser, and hangs it on the node `0x007cfb90 + id × 20`; after the last it rebuilds the free list
from every node with no thing, in rising id (10,239 ids). So a file may use any ids it likes, a thing's id is
its place in no array, and nothing of a thing but what its serialiser writes survives a save. Then it calls
`FUN_005408f0` when `mWorldState` is 4 and `FUN_005408e0` otherwise (not decoded).

### What ties the modules together

Measured with `census.py` over the ten park files, and each a rule a writer must keep when a thing is made or
gone:

- **Ids.** In every file the highest id is the number of things (42 of 42, 526 of 526, 12 of 12): the original
  uses a freed id again. The list mostly falls by id and is not sorted (396 of 525 steps fall in the played
  park).
- **The message sets** (`MESS`) follow the thing list by model, in all ten: set `0xa` holds every guest and every
  member of staff, and the one model-17 thing; `0xb` every catalogue object and the challenge manager; `0xc` every
  member of staff and things of models 9, 12, 13 and 16; `0x1b` every guard and the model-17 thing. The other
  sets hold singletons only. Each set is written in rising id.
- **Sprites** (`SPSC`). A person's record names its slot; the slot's handle is only tested against nought on the
  way in (the Handles file), and the record's `+0x14` is pointed again at the built-in programs. A played park
  holds more live slots than people (435 and 439 against 392): a balloon, a rider's head and the like take slots
  too, and those are not walked here.
- **Scripts** (`RSSE`). One record per script, found by the handle an object's `mRideScriptHandle` holds; a played
  park holds 130 for 124 objects, its companions. The header's tick and next handle carry on from the file. Its
  fourth dword is not the count the reader walks by: the Instant Action save reads 28 there over fourteen
  records, and loads.
- **Models** (`RSYS`). An object's model handle is its slot plus one; a slot is one byte when empty.
- **Track rides and coasters** (`TRAK`, `COAS`) are found by `mTrackRideHandle` and `MeshInstanceID`.
- **Clocks.** Every deadline in `RSSE` and every stamp in `RSYS` is a reading of the clock `CLOK` holds, so those
  three move together. `mGameTick` is not tied to them (the Tick 5000 file).
- **The cells.** A thing's `mMapChild` and `mMapParent` and a cell's `mWho` are one chain of ids per cell, and an
  object's footprint is in the cells' types, parents and occupants.

### Module by module

**Carried** means the bytes of the file the park was loaded from go out again as they were: nothing OpenTPW runs
changes what they say, and they keep what OpenTPW does not model. **Patched** means carried, with the fields
OpenTPW runs written over and records added and taken out. **Afresh** means written from the running park alone.

| Module | Holds | Play here changes | A writer here |
|---|---|---|---|
| The action recording | `mLoadedPublishedPark`, a length, the recorder's buffer | nothing: no action is recorded here | carried |
| `WRLD` | the park | nearly all of it | patched, part by part (below) |
| `SPSC` | a 280-byte record per sprite | a guest or hire made, one gone, every position and frame | patched: a slot filled for a person made, emptied for one gone |
| `PART` | the live emitters and the effect library | emitters of things bought or sold | carried; a thing bought or sold with an emitter is counted |
| `MESS` | 29 listener sets | every set a made or gone thing belongs to | afresh from the thing list for sets `0xa`, `0xb`, `0xc` and `0x1b` by the rule above; the singletons carried |
| `CLOK` | two clock readings | the clock runs | afresh: the file's readings plus the game time run since the load, so every carried deadline keeps its distance |
| `VANT` | one reading of the real-time clock (`FUN_005f5f10`); the reader keeps its distance from its own | nothing | carried |
| `GSYS` | nine dwords (above) | nothing read here | carried |
| `RSYS` | a record per model slot, its channels | every running clip; a slot for a thing bought, one freed for a thing sold | patched |
| `TRAK` | track rides, their sections and cars | a Hot Pot or another bumper ride bought or sold | carried; a track ride bought or sold is counted until a car's 208 bytes are decoded |
| `FLYR` | the flyers (FileFormats `saves.md`) | nothing built here | carried |
| `RSSE` | every running script | every script's counter, variables, stack and deadlines; a script for a thing bought, none for one sold | patched |
| `KAME` | zoom, rotation, flags, two points of interest, a saved rotation | the camera | afresh |
| `COAS` | the coasters | nothing: none can be built here | carried |
| `ADVS` | 360 bytes of the advisor and a buffer | nothing kept here | carried |
| `SOUN` | two dwords, 84 bytes, then records not decoded | nothing kept here | carried |
| `CHTS` | two bytes | nothing | carried |
| `ADSC` | one dword | nothing | carried |
| The UI block | a count and 540 bytes for each message standing | nothing: no message stands here | carried |

**Inside `WRLD`:**

| Part | Play here changes | A writer here |
|---|---|---|
| The header | `mGameTick`, `mParkClosed`, `mNumberOfVisitorsToDate`; the five staff heads and `mFirstObject` as things are made and gone; the arrival vehicles' handles | patched. `mRandomSeed` is carried: the generators here are not the original's |
| The object controls | an item's standing count and first-build stamp, its researched flags | patched |
| The staff pool | every candidate | afresh from `ParkStaffPool` |
| The clock and arrival fields | the date, the arrival timer | patched |
| The cells | a path or queue laid or cleared, land, a footprint, the chain of who stands where | patched cell by cell; a cell's record gains or loses its map, track and effects parts by its status bits |
| A guest, a member of staff | everything they do | carried records patched from `ParkPeople`; one made here written whole; one gone left out |
| A catalogue object | its door, price, counts, rings, queue, script handle | carried records patched; one bought here written whole; one sold left out |
| The economy thing, the staff HQ | the balance, the loans, the rings; the strikes | patched |
| The analyser, the research lab, the weather, the challenge manager, things of models 10, 11, 12 and 17 | what the calendar and weather run here | carried, patched where a field is run here |

**What is not settled, and belongs to the build that meets it.** A record made here has bytes no reader here
names: whether nought is safe in each is to be measured by loading a made guest and a bought ride in the
original. A person in the middle of a walk carries the original's navigator (177 bytes), which OpenTPW's walk does
not keep. A park that was never loaded from a file (the fresh world) has nothing to carry: every module would be
afresh, the legal text taken from the level's shipped park file.

### OpenTPW's writer, the first stage

`ParkFileWriter` (Q241f) writes a park file from the one the park was loaded from. `ParkWorld` keeps the inflated
body it read and the file's first `0x60D` bytes (`SaveReader.Preamble`); the writer copies the body and writes
over the copy, never the one held.

- **The container.** The version, 500, whatever the file loaded carried (the shipped park's 400 goes out as 500,
  as the original's save of it does); the loaded file's preamble after the version, where the original writes its
  running language's legal text; `BILZ`, the body's length, the block's length, then 15, 9, 0, 0; and the body
  through .NET's `ZLibStream`. **A deviation:** the stream is not the original's byte for byte, having no memory
  level 9 ("The writer", "Measured"); the header still reads 9, and the original loads it.
- **Written from the running park:** `mGameTick`, `mParkClosed` (1 or 0), `mNumberOfVisitorsToDate`, the economy
  thing's `mBalance`, and of the camera module the zoom, the rotation and the point's two ground coordinates. The
  camera's flags, the point's height, the saved point and the saved rotation are the file's.
- **The rotation is written as the orbit camera's yaw negated.** Measured: at nought the two games show the same
  view; a file holding +π/2 for OpenTPW's quarter turn put the original's camera on the far side of the point, and
  one holding -π/2 on the same side as OpenTPW's. OpenTPW turns an eighth a press (Q23), which the original's
  loader rounds to a quarter (above, "Loading the modules", Camera).
- **Everything else is carried**, so a park loaded from the file has the people, objects, ground and scripts of
  the file it was first loaded from, under the new clock, count, cash and camera. Stages two to five write the
  rest (Q241g to Q241j).
- **Where.** `Level.WritePark( name )` writes `<player's folder>/<theme>/<name>.TPWS`, replacing a file of that
  name in another case. The console's `savepark <name>` is its one caller; the Save Park screen's OK stays counted
  until Q241j. **Deviations:** the player's `gms.dat` is not written first (Q248) and the pointer is not put back
  to its default mode; a park made fresh, with no file behind it, is counted (`SAVE_PARK_WITH_NO_FILE`, Q249) and
  not written; with nobody playing, which only the console's `park` reaches, nothing is written.
- **The load does not read the camera module**: a park loaded here opens on the default view (Q247).

**Measured (Q241f, `q241f/`).** Lost Kingdom entered by a player holding `easymode.TPWI` alone (755, 87987, no
visitors), stepped under `pause` to `mGameTick` 1000, the camera at `camera 480 280 90 90`, then `savepark Q241f`:
the log read "mGameTick 1000, open, 13 visitors to date, balance 88112"; a Python reader found version 500, the
lengths closing, those four numbers and the camera in the body, and 15 bytes of the body differing from
easymode's, none outside the fields written. Loaded from OpenTPW's Load Park screen the clock started at 1000 and
the balance read 88112. **The original under Proton** listed the file and loaded it over a park standing at tick
1836 and $ 88162: `mGameTick` read 1000 at the click and 1150 forty seconds on, the visitor count 13, the cash
$ 88112 on the first frame, the zoom 90, the rotation -1.5708 and the point (480, 280), the frame from the same
side as OpenTPW's photograph. Three runs: fifteen predictions of fifteen here, and two of mine wrong in the
original, the rotation's sense (the first file held +1.5708) and an eighth turn standing (it is rounded).
**Seen and not built here:** the thirteen saved guests are back outside the gate in the file, so after a load
they walk in and are counted again, 14 to 26, in the original; the people are Q241h's.

### Read, not run

A thing made or gone was not written and loaded: the rules above are the ten files' and the listing's. The
readers of the sound module, the advisor scoring and the UI block were not read, only their writers. What
`VANT`'s clock is for, what the action recording is read by after a load, and `FUN_005408f0` are not traced. The
fresh world's save was not looked at. `addresses.md` is not regenerated.

**The harness** is `q241e/`: `ghidra/` (the dumps of every module's writer and reader), `roundtrip/` (the .NET
container), `patch.py` (one change to a body), `census.py <park file>...` (the ties above, read-only),
`PREDICTION.txt`, and `orig/` (the four logs, the frames `l1`, `l3`, `h2`, `c1`, `PREDICTION-result.txt`).
Q241f's is `q241f/`: `confirm.py` (the write and the load, with its own reader of the file), `PREDICTION.txt`
(the three runs and their results), `mutate.py`, `orig/` (`read.py`, the three load logs, the frames) and the
sheet of OpenTPW's frames beside the original's.

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
| The park file written back, first stage; the camera module | `OpenTPW.Files/Formats/Save/ParkFileWriter.cs`, `ParkCameraModule.cs`; `Level.WritePark`, `SaveFolder.WritePark` |
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
