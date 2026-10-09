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

Decoded and measured on 2026-10-08 (Q241e). `ParkSaveScreen.Save` still counts `SAVE_GAME_WRITER`; three of the
writer's five stages are built ("OpenTPW's writer, the first stage", "the cells" and "the people", below), with the
pool of candidates and the arrival timer ("the staff pool and the arrival timer"); the fourth's decode is "The
objects, their scripts and their models". The bytes are the FileFormats
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
5. `FUN_004d7ea0`: the 16,384 cells (`mMap`), on the world's member at `+0x2d8` (`LEA ECX,[EDI + 0x2d8]`,
   `0x005175a2`): a cell's 0x44 bytes at `+0`, its track cell's 0x28 at `+0x110000`, its ten bytes of effects at
   `+0x1b1104`. **Every cell is written with its map and its track record**: the status byte takes bit 1 and
   bit 2 when `FUN_0050c1f0` says the cell differs from a default one built beside it (`FUN_00536490`,
   `FUN_0053af00`), and `FUN_0050c1f0` is `return 1`. Only bit 4 is a test: the ten bytes against a default's
   (`FUN_00502470`). So a file the original wrote opens every cell with 3 or 7, and the three log lines that count
   "cells as default" count none for the map and the track. The reader takes each part its bit names and, after
   the last cell, zeroes the 33 x 33 block stamps at `+0x1b0000`.
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
- **Models** (`RSYS`). An object's model handle is its slot plus one; a slot is one byte when empty. A slot's
  record names the script of the thing it is the model of, and a queue cell's model is a slot of the same table
  ("The objects, their scripts and their models", below).
- **Track rides and coasters** (`TRAK`, `COAS`) are found by `mTrackRideHandle` and `MeshInstanceID`.
- **Clocks.** Every deadline in `RSSE` and every stamp in `RSYS` is a reading of the clock `CLOK` holds, so those
  three move together. `mGameTick` is not tied to them (the Tick 5000 file).
- **The cells.** A thing's `mMapChild` and `mMapParent` and a cell's `mWho` are one chain of ids per cell, and an
  object's footprint is in the cells' types, parents and occupants. The reader takes all three as the file has
  them (`FUN_0050b090` reads the two links into the thing's `+10` and `+8`, the cell's reader `mWho` into its
  `+0x24`), so a cell's `mWho` can only be written with the records of the things it names. **A load enters
  nobody in a cell**: the reader's constructors for a guest and for the five staff (`FUN_00518e00`,
  `FUN_00518e20`) zero the event ring (`FUN_0050ba80`) and build the navigator (`FUN_0050ffe0`), the id is set
  (`FUN_0050b080`), and the serialiser reads the rest, links and all. **Every thing is in one chain, and a person
  in the chain of the cell their `mX` and `mY` name, ahead of any object there**: 18 of 18 in the shipped park,
  392 of 392 and 65 of 65 in the played ones, riders and resting staff with no sprite among them
  (`q241h/chains.py`).
- **The staff lists.** The header's `mFirstHandyman`, `mFirstMechanic`, `mFirstEntertainer`, `mFirstGuard` and
  `mFirstResearcher` each name the first thing of the model in the thing list, and each member's `mNext` the
  next of it: the lists run in the list's order, newest first, in all ten files.
- **The sprite table grows by fifty.** A new sprite takes the lowest empty slot from 1 and stores it in its own
  `+4`; with none empty the table is made fifty slots longer (`FUN_00475a10`, `FUN_00475cf0`). The reader makes
  its table of the file's own count (`FUN_00475730`).
- **A queue cell's model.** A queue cell's `mMeshInstance` (the cell's `+4`) is the handle of the model the retile
  made for it (`FUN_005365d0`: tile set 2 frees the handle held and stores `FUN_005229e0`'s). No load calls the
  retile, so the handle in the file is the one used: 4 of 4 queue cells in the shipped park and 78 of 78 in a
  played one hold one, and no other cell of either does (`q241g/cells.py`). The handle is a slot of `RSYS` plus
  one (below).

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
| The staff pool | every candidate, the top-up's counts and mark | written slot for slot from `ParkStaffPool`; an empty slot keeps its bytes and loses `mValid` |
| The clock and arrival fields | the date, the arrival timer | patched: the timer's mark, count and flag are written; the clock's fields are still the file's (Q241j) |
| The cells | a path or queue laid or cleared, land, a footprint, the chain of who stands where | patched cell by cell, each record where it lies (every cell has its map and track parts); `mWho` with the things it names; the effects part carried |
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
- **Everything else was carried** at this stage, so a park loaded from the file had the people, objects, ground
  and scripts of the file it was first loaded from, under the new clock, count, cash and camera. The ground is
  the second stage's ("OpenTPW's writer, the cells"), the people the third's ("the people"), the pool of
  candidates and the arrival timer theirs ("the staff pool and the arrival timer"), the file's objects, scripts
  and models as they run the fourth's ("OpenTPW's writer, the objects"); a thing bought and a thing sold the
  fifth's; the rest is Q254, Q255 and Q241j.
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

### OpenTPW's writer, the cells

The second stage (Q241g). `Level.WrittenCells` hands the writer every cell the running park has changed
(`ParkState.ChangedRecords`) that differs from the file's, and `ParkWorld.PutCells` writes each over its record
in the copied body, where it lies.

- **Written of a cell:** the map record's `mDirection`, `mFlags`, `mNeighbours`, `mOverlapCounter`, `mParentID`,
  `mTileData` and `mType`, and the track record's flags, neighbours, parent and type. **Left as the file's:** the
  status byte, `mMeshInstance`, `mHoardingNeighbours`, the litter block, `mWho`, the track's segment and the
  effects part. A file whose cell has no map record, or track fields to write and no track record, is refused:
  the original writes none such.
- **`mWho` is not written here.** The chain is the cell's head and the things' two links together (above): the
  people's stage writes it for every cell a person stood on or stands on ("OpenTPW's writer, the people"), and
  an object's place in it is the fifth stage's ("OpenTPW's writer, a thing bought and a thing sold").
- **A footprint is not written, and is counted** (`SAVE_PARK_FOOTPRINT_CELL`, one a cell): a cell that has joined
  or left a footprint (types 4, 9 and 10), or changed its type or parent inside one, is left as the file's,
  unless the thing bought or sold is written with it (the fifth stage). An entrance that only gained or lost a link
  is written.
- **A queue cell's model is written with the things** ("OpenTPW's writer, a queue cell's model"): a cell laid or
  tiled again names a model made for it and a cell cleared names none. Where the things go out as the file's
  (no scripts to hand, or a module that did not read), and for a cell on a tile outside the eight pieces, the
  cell is left naming the file's and counted (`SAVE_PARK_QUEUE_CELL_MODEL`).
- **Land** is not bought here (`BUY_LAND_TOOL`), so no cell's `0x40` flag moves but as the path tool moves it.

**Measured (Q241g, `q241g/`).** Lost Kingdom at `mGameTick` 1000, a spur of three laid north off the south road
(`path 45 27`, `45 26`, `45 25`) and a gap of two cleared in the west road (`delpath 39 24`, `39 25`), then
`savepark Q241g`: the log read "8 cells of ground", the three laid, (45,28) which gains its link, the two cleared
and (39,23) and (39,26) which each lose one. A Python reader found exactly those eight cells differing from
easymode's, each reading what the console's `cell` printed, 44 bytes of the body differing and none outside the
fields written. Loaded from OpenTPW's Load Park screen the cells read the same; the unchanged build's file held
easymode's cells and its load put the road back. **The original under Proton** listed the file and loaded it over
its running park: `mGameTick` went from 1028 to 1000 and on to 1168, and its cells, read from memory (world
`+0x2d8`, each cell's own id checked), went from easymode's to the file's on all ten read, field for field: (45,27)
type 1, neighbours `0x11`, tile (1, 2, 0); (39,24) type 0, tile (0, 55, 0). Its frame shows the spur and the gap.
**One prediction wrong, mine:** ten cells; the road cells either side of the spur's foot gain no diagonal bit.
**Found there:** the zoom read 70 where the file holds 60 (not decoded: a floor in its loader or its camera), and
the cash read $ 88177 seven seconds on, the saved guests paying at the gate again (Q241h).
**Not run in the original:** a queue cell written without its model, an entrance's link, a cell laid over.

### `mTimeHired`

A member of staff's `+0x1f0`, eight bytes, file 491. **It is the park's calendar as the member was made, a
`FILETIME`.** The staff constructor's tail `FUN_00504b90` calls the calendar's now, `FUN_004f8690` on world `+0x2a0`
(`0x00504bb8`), and stores both dwords (`0x00504bc4`, `0x00504bd6`); a load reads the file's over it. `FUN_004f8690` is
`mFunnyTimeStart` + `mGameTick` × `mFunnySecsPerRealSec` / 4 × 10,000,000 (`0x00989680`), added as 64 bits by
`FUN_005fc530`, and only broken into a date for a caller that passes somewhere to put one (`FUN_005fc660`,
`FileTimeToSystemTime`); this caller passes nought eight times. **One reader:** `FUN_00505b70`, now less `+0x1f0`
(`FUN_005fc550`) over 864,000,000,000 (`0x00c92a69c000`), the days employed, which the staff window's refresh
`FUN_004b58b0` hands to UITEXT `0x1bb` (`0x004b58d5`).

Measured: the shipped park's four first members read 1.1.2000 15:37:30, tick 15 at 3,750 s a tick, and its
researcher 29.1.2000 03:00:00, tick 648; all 53 of the played jungle park's and all 12 of the fantasy park's are a
whole number of ticks, no two alike, the newest thing the latest (`q251/look.py`). OpenTPW stamps a hire with
`ParkState.CalendarNow` as `ParkPeople` makes the member, reads the file's (`ParkWorld.StaffState.TimeHired`) and
writes both back; the console's `staff` prints it. Nothing here shows the days employed: the staff window is unbuilt.

### A sprite's `+0xbc`

`FUN_00540c60( bank, n )` answers the byte at the bank object's `+0x222 + 4 × n`. `SpriteBank_Load` reads the sixteen
sets of the `.esp` (file `0x10E`, four bytes a set) to `+0x220`: the first picture's word, **the frames a direction at
`+0x222`** and the directions flag at `+0x223` (`0x00540ee3`..`0x00540f1d`), which is how `Sprites_LookUp` uses them
(`0x005423f0`). The byte is written to a sprite's `+0xbc` in three places and no other:

| Where | With |
|---|---|
| the constructor `FUN_004758f0` (`0x004759f2`) | the bank of the kind and bank it is made with, and **the set it is made on**, its fifth argument |
| `FUN_00475b80` handed a state 0 to 3 (`0x00475c16`) | the set that state's group has just written to `+0xb4` (`FUN_00540c70`). Handed a script's address, it leaves `+0xbc` alone |
| `FUN_00475e10` (`0x00475edd`) | a copy of another sprite's |

**A script that changes the set does not write it**, so it is not the frames a direction of the set at `+0xb4`: a
walking child on set 1, eight frames a direction, holds 1, set 0's. The sprite script reads `+0xbc` as one of its
locals (`SpriteScript.FramesPerDirection`), which is what a state's animation steps its frames by. A bubble is made
by `FUN_00475a10( script, 9, 0, 0, ... )` (`0x0050c062`), on bank 0's set 0 whatever its picture, and its script sets
the picture; so a bubble whose `+0xb4` is 16 or more (the second bank's) still holds bank 0's set 0's byte, and
nothing reads past a bank's sixteen sets. A balloon is made on its colour's set.

Measured over every live sprite of the six park files to hand that hold any, 1,053 (`q251/fpd2.py`, each `.esp`'s own
bytes): a person's `+0xbc` is their bank's set 0's, but for 26 of the 27 entertainers, who hold their group's set's (8, or 17
on `SPR_EX`), having performed; the one who holds set 0's is the shipped park's; every balloon holds its own set's, 2;
every bubble 1; the litter and effects sprites their own set's. None differs.

### OpenTPW's writer, the people

The third stage (Q241h). `ParkPeople.Written` hands the writer every guest and member of staff the park holds,
and `ParkWorld.PutPeople` puts the thing list together again around them, with the three parts of the file that
follow the list. It runs last, because it alone changes the body's length.

- **The thing list.** A person the file holds and the park still has keeps their record, written over field by
  field. One made here is written whole, ahead of the file's things and newest first, the park's own turn order.
  One gone is left out. Every other thing's record goes out as it lies. Each record's first dword and `Used
  Thing Head` are written from the new order. **A deviation:** a made thing's id is the park's own, one past
  the highest it has given, where the original gives a freed id out again (Q26c); the reader takes any id.
- **A record's fields** are the FileFormats page's, from `ParkPeople`: `mX` and `mY`, the slot, `mNextAnim`,
  `mNextServiceInterval`, `mAccurateDestX`/`Y` (the target in `mX`'s units, as 18 of 18 and 392 of 392 hold it),
  the four speeds, `mCount`, `mESPSprite`, `mSpriteID`, `mPreviousX`/`Y` (the navigator's fixed point),
  `mStrandedTime`, `mSetDestSuccessfully`, `mSpriteAngle`, the thought and its stamp; the navigator's position,
  velocity, target, limits, counts, distances, stuck bits and, where a route was planned here, its waypoints and
  legs; then a guest's block or a member's, `mArrivalIndex` among them (the guest's number among the park's
  visitors, which the reader now gives back to `Peep.VisitorNumber`).
- **What a made record leaves at nought** is what the original's own constructors leave there: the navigator's
  force, formation, axes, mode, last progress and timestamp, `mLastRecordedMapId`, `mSpriteUnderRideCtrl`, the
  event ring, a guest's tiredness. A hire's `mTimeHired` is the park's calendar as they were made ("`mTimeHired`",
  below).
- **The chains.** A person is written on the cell their `mX` and `mY` name. Whoever has come onto a cell heads
  its chain, the newest first; whoever the file had there and is there still follows in the file's order; then
  the file's things that are no person. `mWho` is written for each cell whose head changes.
- **The staff lists** are written from the list's order, as the files' run.
- **The sprite table.** A kept person keeps their slot; a made one takes the lowest empty, and the table grows
  by fifty when it is full; a gone one's is emptied. A made record is the constructor's (`FUN_004758f0`): its
  slot, state 1, the timer `0x14`, alpha 255, scale 1, the program and the place, and nought in `+0x7c`, a time
  already past. A live slot's handle is the slot's own number, which the reader only tests against nought.
  **`+0xbc` is the frames a direction of the set the sprite was made on** ("A sprite's `+0xbc`", below): a made
  person's is their bank's set 0's, and a state's animation started since writes its own set's over a made
  sprite's and a kept one's alike. Only where the bank is not to hand is it copied from the nearest sprite the
  file holds (the same kind, bank and set, then the same set, then the same kind), and counted where there is
  no such sprite (`SAVE_PARK_SPRITE_SET_BYTE`).
- **A balloon and a thought bubble** are each a sprite of the table, named by the slot at the guest's
  `mBalloonScript` and the person's `mThoughtScript`. One the file holds for that person, of that kind, which
  the park still shows keeps its slot and is written over; one made takes the lowest free slot after the
  people's own, the lower id first; one gone is let go. A balloon is kind 10, bank 0, on the script, the set,
  the frame and the alpha it is on, where it was last placed, with its interval. A bubble is kind 9, bank 0,
  its picture at `+0xb4`, frame 0, over the person at 2.5, on its picture's script four words in, where one
  that has shown its frame rests. The 22 thought scripts are six words apart from word 1462 (`0x0074f190`):
  set the picture, show frame 0, jump back; they set pictures 0 to 15 in order, then 21, then 16 to 20
  (`Thoughts.ScriptOf`). The bubble here runs no script, so it is written as one that has run its first two
  instructions, which all but one of the 46 saved bubbles to hand have.
- **The message sets.** `0xa`, `0xc` and `0x1b` hold every person, every member of staff and every guard beside
  the file's members that are no person, in rising id.
- **A guest on a thing is written as they are** (queueing, called forward, walking on or off, riding), with
  their thing, their place and their links in its queue: the thing's own half is written with the things
  ("OpenTPW's writer, the objects").
- **Deviations, each counted.** A handle to a thing the file does not hold, which
  is anything bought here, is written as nought, and a guest bound for it or on it decides where they stand
  (`SAVE_PARK_HANDLE_TO_AN_UNWRITTEN_THING`, and `SAVE_PARK_GUEST_ON_A_THING` for one on it). A balloon let go and still bursting is nobody's and is not
  written (`SAVE_PARK_BALLOON_LET_GO`). A member
  of staff in the hand is written idle where they were picked up, as the original puts the hand's thing down
  first.
- **The staff pool and the arrival block were not written at this stage**, so a load was followed by a load of
  arrivals: both games called one of thirteen within 30 s of loading a file written at tick 1334, where the
  park written was about 580 ticks from its next. They are written now (below).

**Measured (Q241h, `q241h/`).** Lost Kingdom left alone until its first load of thirteen was walking in from the
stop, a researcher hired at (45,28), held at `mGameTick` 1334, `savepark Q241h`: the log read "18 people kept, 14
made, 0 gone; 32 sprites in 100 slots", and a Python reader found 56 things, 26 of them guests, every chain whole
with all 32 people on their own cells, 33 ids in set `0xa`, and each of the 32 within 0.0005 of a cell of where
`peeps` and `staff` had them, on the program, counter, set and frame `guests` printed. Loaded from OpenTPW's Load
Park screen the clock started at 1334 with all 32 in place; 45 s on no visitor number was held twice, the guests
of the file held their own and the made held 14 to 22, the rest still outside. The unchanged build's file held
easymode's 18 people, and 45 s after its load those thirteen guests had been counted again, 14 to 26.
**The original under Proton** listed the file and loaded it over a running park, twice (two builds' files):
`mGameTick` read 1334 at the click and counted on, the visitor count 13, its node table 56 things with 26 guests
and two researchers; 30 s on all thirteen made guests stood elsewhere and the hire had walked from (47.4,28.3) to
(42.4,28.4); 60 s on the made guests held arrival indices 14 to 26, ten of them in a queue, one stepping up it and two riding, and
every guest of the file's held the index the file gave. Left running, the first file's park reached tick 11,735
and 180 visitors. **Predictions wrong, mine:** a visitor count that "never passes 26" (the next load came, in both
games); 32 people at the save where one run had 31 (a guest of the file's had gone home); and two of my own
checks of OpenTPW's load, which read a sweep after it (walkers 0.1 to 0.5 of a cell on, one guest setting off).

**Measured (Q251, `q251/`): the balloons, a bubble, the hire's date and the set bytes.** Lost Kingdom with a Balloon
Shop bought at (43,22) and a researcher hired under `pause` at `mGameTick` 783, left until its first load of thirteen
had been made and nine guests held a balloon, the hire tired with `staffrest` and the park held on the first poll that
showed their bubble, `savepark Q251` at 1329: the log read "17 people kept, 14 made, 1 gone; 41 sprites in 100 slots,
9 of them balloons and 1 bubbles", and neither `SAVE_PARK_SPRITE_SET_BYTE`, `SAVE_PARK_BALLOON` nor
`SAVE_PARK_THOUGHT_BUBBLE` was counted. A Python reader found each of the nine holders naming a kind-10 sprite of the
set `guests` had printed, `+0xbc` 2, on slots 32 to 40, and nobody else naming one; the hire naming a kind-9 sprite
on slot 41, picture 18, script 1576 at 1580, 2.5 up, `+0xbc` 1; the hire's `mTimeHired` 3.2.2000 23:37:30, which is
783 x 3,750 s, and the file's five their own; and all 41 sprites' `+0xbc` by the rule above, the thirteen made
children 1 and the researcher 4. Loaded from OpenTPW's Load Park screen the same nine held a balloon of the same set
and `staff` printed the hire's date again. The unchanged build's file held no kind-10 and no kind-9 sprite and a
nought for the hire's date, counted `SAVE_PARK_BALLOON` eleven times, `SAVE_PARK_SPRITE_SET_BYTE` four and
`SAVE_PARK_THOUGHT_BUBBLE` once, and after its load nobody held a balloon. That is the third run of five; the
commit's own build read the same way at tick 1316 with eight balloons (`gate2`).
**The original under Proton** loaded two of the files over a running park (`orig/a-load.log`, `b-load.log`): on its
first read that showed the file's `mGameTick`, each holder's `+0x210` named the file's slot and
that slot held a kind-10 sprite of the file's set, `+0xbc` 2, state 2 and shown; the hire's `+0x1f0` read the file's
`FILETIME`; and the hire's `+0xb4` named slot 41, a kind-9 sprite on script 1576 at 1580 showing picture 18. Its
frames show the balloons over their guests.
**One decode wrong, mine, and the original caught it:** the first file wrote the bubble's script as 1462 + 6 x the
picture, 1570 for picture 18; the original ran it and read picture 17. The scripts set 0 to 15, then 21, then 16 to
20, as the executable's words and the played saves' own pairs say; the checks of the first two runs carried the same
mistake and passed. **Two predictions wrong, mine:** a saved bubble counted at OpenTPW's load (the bubble was a
member of staff's, whose thought a load does not read at all; only a guest's is counted, `SAVED_THOUGHT_BUBBLE`), and
no bubble at all after the load (in one run of five a guest thought anew on the sweep after it).
**Found by tests, not runs:** a slot a record names that holds a sprite of another kind was written over (it is
left and a new slot taken); and a hire put down thinks "very happy" on one draw in sixteen, a bubble that is now a
sprite of the file.
**Not run in either game, tested only:** a balloon or a bubble the file already held (kept on its slot, or let go),
a guest's bubble, an entertainer's set byte after a performance, a balloon bursting at the save (counted), a slot
naming another kind's sprite. **Not looked at:** the original's staff window and its days employed for the hire; the
bubble was read from memory and is in no photograph of the original (it is freed 12 sweeps past its stamp).
**No bubble shows after OpenTPW's own load**: the reader makes none. `docs/exe/addresses.md` not regenerated.

### OpenTPW's writer, the staff pool and the arrival timer

Split from the people's stage (Q250). Both blocks lie before the map, so they are written where the file has
them and move nothing.

**Every stamp in them is a reading of `mGameTick`.** `FUN_0041a960` sets a mark to the clock
(`[[0x0080239c] + 0x1da70c]`), `FUN_0041a970` answers the clock less the mark, and `FUN_0041a990` the same in
fours (`park.md`, "Arrivals"). The pool's turn `FUN_005084f0`, once a sweep, takes each valid candidate not on
the pointer whose `mTimeoutTime × 4` is less than the sweeps since their `mTimeSig`: it clears `mValid` alone
and tells the hire list (`FUN_00481550`). Then, when the pool's own mark (world `+0x294`) is more than
`TimeBetweenStaffUpdates × 4` sweeps old, it counts the staff (`FUN_00508000`), tops the pool up, each newcomer
into the lowest slot with `mValid` nought (`0x0050876f`), makes up the minimums and sets the mark.
**`mPeopleInCat` and `mStopProducing` are that count as the last top-up left it**: `FUN_00508000` zeroes the
five dwords at `+0x280` and the five bytes at `+0x299`, walks the thing list and counts each member by model,
5, 4, 6, 7 and 8 into slots 0 to 4 (the kinds' own order), setting a kind's byte once its count reaches its
`Max<Kind>InPark`. So a file holds the staff as they stood at the pool's mark.
`mOpeningStaffPoolGenerated` (`+0x298`) follows the mark in the file.

So a file whose clock is written and whose marks are not is a park whose waits have all run out, which is what
the people's stage wrote.

- **The pool** (`ParkStaffPool.Written`, `ParkWorld.PutStaffPool`). Each candidate keeps the slot the file had
  them in, and one who joins here is given the lowest slot nobody holds, as the original gives it. A slot with
  a candidate is written whole: kind, name row, grade, costume, valid, off the pointer, their mark and their
  lifetime. A slot with none loses `mValid` and keeps its other bytes, as the original's does. The five counts
  and flags are the pool's as its last top-up took them, the file's own until one has run; the mark is the
  pool's. `mOpeningStaffPoolGenerated` is the file's. A candidate on the cursor is written as any other: the
  original puts the hand's thing back before it writes.
- **The arrival timer** (`ParkPeople.WrittenArrival`, `ParkWorld.PutArrival`). `mTimeSig`, `mPeopleOnBus` and
  `mOffloading` are the running park's; `mArrivalRate`, `mTargetVehicleCapacity` and `mGatesOpen` are the file's
  (0, 5 and 1 in every file measured; nothing here runs them).
- **A load held is carried on by a load here**: `ParkPeople` takes the count and the flag as the manager's turn
  reads them (`FUN_004cf3e0`), and the rest are dropped once a vehicle answers that it is unloading.
- **A deviation, counted: the vehicle is not written** (`SAVE_PARK_ARRIVAL_VEHICLE`, once a save with one
  current). The header's `mCurrentArrivalVehicle` and the vehicle's script are one state: `FUN_0051a690` reads
  the script's variable 1 of the thing the header names (`park.md`, "Arrivals"). The bus's script is written
  as it runs ("OpenTPW's writer, the objects") and the header's handle is still the file's (Q255), so a park
  saved with the bus on its circuit loads with no vehicle current and its bus driving on. A load held is then brought by the
  vehicle its size summons, which drives in again; a guest by the road has no vehicle to wait for.

**Measured (Q250, `q250/`).** Lost Kingdom left alone from easymode's 755 to `mGameTick` 1453 (1455 in a second
run), `savepark Q250`. Before the save the log held the pool's top-ups on 1083 and 1444 and the first load's
let-go on 1313; the save read "20 candidates in the pool, topped up on mGameTick 1444" and "the arrival timer's
mark 1313, no load held". A Python reader found 20 valid records, each kind, grade, mark and lifetime a line of
the `candidates` census, the pool's `mTimeSig` 1444, the counts 1 five times, and the arrival block
(0, 1313, 5, 0, 0, 1). Loaded from OpenTPW's Load Park screen, `arrivals` read mark 1313 and "next load due on
mGameTick 1916" and `candidates` the same twenty; run on, the pool was topped up on 1805 and a load of twelve
called on 1916, and neither before. **The build before** wrote the shipped pool and timer (722, 661) under
tick 1454: after its load `arrivals` read mark 661, a load of twelve was called and the pool topped up on 1455,
the first sweep, and ten of the save's twenty candidates were left.
**The original under Proton** loaded the first run's file over a park standing at tick 830: at the click
`mGameTick` read 1453, the arrival block (0, 1313, 5, 0, 0, 1), the pool's mark 1444 with 20 valid records and
the counts 1 five times; its Hire Janitors tab listed the five cleaners of OpenTPW's own screen at the same
wages; its candidates timed out one by one, to ten by 1752; the pool's mark read 1805 on `mGameTick` 1805, with
twenty again; and `mOffloading` read 1 on `mGameTick` 1916 with `mPeopleOnBus` 12, dropped one a sweep from
1947 and let go on 1959. **One prediction wrong, mine:** no vehicle current at the save; the bus was, at status
3 on its way round, so `SAVE_PARK_ARRIVAL_VEHICLE` was counted once. **Seen in the original and not
predicted:** `mCurrentArrivalVehicle` went from nought to 32 on the first sweep after the load, a vehicle made
anew, then 40 on 1583, the bus on 1768, nought on 1896 and 32 on 1898, which brought the load: the tail's
summons for a guest waiting at the stop to go home (`FUN_004cf3e0`, read; the guest was not looked for).

### The objects, their scripts and their models

Decoded and measured on 2026-10-08 (Q241i, the decode; nothing of it is built). An object is three records that
name each other: its thing record in `WRLD`, a script record in `RSSE` and a model slot in `RSYS`. The bytes are
the FileFormats `saves.md`'s ("The ride script module", "The ride system module"); this is what the original does
with them and what the writer's next stages must keep.

**The model table.** `FUN_00463060( type, flags, { x, y, angle, ... }, ..., handle, restoring )` makes every model
instance and answers its handle, the slot plus one. Asked for no slot (`-1`), it takes the slot at the cursor
`DAT_007aedf4`: when that is the count of present slots `DAT_007a4084` both go up by one, and otherwise the cursor
moves on to the next empty slot, the free count `DAT_007a3ebc` goes down and the present count up. The three
globals are the module's header, in the order present, free, cursor (`FUN_00464140`): the shipped park reads 161,
7, 90 with slots 90 to 96 empty, and in all six files the cursor is the lowest empty slot, or the count where
none is. The slots are the pointers at `0x007a4614`.

**The reader makes every model again** (`FUN_004647a0`): for each present record it finds the model type whose
`+0xf8` is the record's item id (the list at `0x007ae6a8`), and calls `FUN_00463060` with the record's own flags,
cell, angle and footprint, **the slot it lies in plus one for the handle**, and 1 for restoring. Then it lays the
saved state over the fresh model: the hoarding bits and progress, the lookup records' flags and attached handles,
the nodes' flag words, and each channel. **The node and lookup counts are tested first** (`param_1 + 4` and
`+ 6` against the model's `+0x42` and `+0x48`): where they differ it logs "Node Embedded node count changed" and
steps over both tables by the FILE's counts, keeping the fresh model's own. The channels are read by the model's
count (`+0xe`), which the record does not hold.

| Record | Model | What it is |
|---|---|---|
| `0x01` | `+0xf8` | the item id |
| `0x05`, `0x09` | `+0xe8`, `+0xec` | the cell: an object's `mTopLeft`, a queue piece's own cell |
| `0x0d`, `0x11` | `+0xf0`, `+0xf4` | the footprint, cells across and down (whether turned with the angle was not measured: no turned object to hand is longer one way) |
| `0x15` | `+0xe4` | the flags it was made with: `0x32f` a placed object, `0x361` a fixed one (gates, lights, a vehicle), `0x33a` a queue piece |
| `0x19` | `+0xd4` | **the script handle of the thing it is the model of**; nought for a queue piece and the other scenery |
| `0x27` | the mesh's `+0x10` | the angle: an object's `mAngle`; a queue piece's 360 less its tile's angle |

Measured over all six park files to hand that hold objects (`q241i/rsys.py`): an object's `MeshInstanceID` names
a present slot whose record holds its item, its script handle, its angle and its `mTopLeft` cell, 360 of 360
(the cell 337 of 337 placed). So two things of one item are told apart: the three Small Toilets' records name
scripts 11, 12 and 13, and `ParkRides.PairSavedThings` pairs each thing with the record that names its script.

**A queue cell's model is a slot of the same table.** The retile `FUN_005365d0`, for tile set 2, frees the handle
the cell holds (`FUN_00522a90`) and stores `FUN_005229e0`'s, which is `FUN_00463060( [0x00763388 + 12 x tile],
0x33a, { x, y, 360 - angle }, ..., -1, 0 )`: a new slot at the cursor. Its record's item is 17000 plus the tile's
index (17002 to 17007 in the files), its cell the cell's: 4 of 4 queue cells in the shipped park and 78 of 78 in
a played one name such a slot, at their own cell. The other records, the most of them (items 173xx in the shipped park,
143 of its 161, and 171xx too in a played one), are named by no thing and by no cell's `mMeshInstance`; what
makes them was not looked for.

**A script's record is its struct, raw, and its tables** (`FUN_00559350` writes, `FUN_005597a0` reads). The
loader `FUN_005587f0( path, thing, quiet )` makes the struct: all nought, then the counts from the `.RSE` header
(variables `+0x8c`, stack `+0x54`, limbo `+0x58`, bounce `+0x64`, walk slots `+0x7c`, body `+0x50`, strings
`+0x90`, and one more of the header's dwords into `+0x94`, 50 in every record), the call index `+0x40` at the stack's
size less one, **the handle `+0x08` from the counter `DAT_008791a8`, which only counts up**, 1000 in the words
at `+0x88`, `+0x8a` and `+0xe4`, the speed word 50 at `+0xc0`, 1 at `+0x70`, -1 at `+0x74` (the name's offset)
and `+0xd8`, `0xffff` at `+0xa8` and `+0xe6`, the thing at `+0xac`, **the thing's model handle at `+0xc8`**
(the thing's `+0x20`), and one head slot for each head node of the model (`+0x4c`). The reader reads the struct
back whole and then replaces what are addresses: the list links, the nine table pointers (each table read from
its own block: the body, the stack, the variables, the strings, limbo at 8 bytes a slot, bounce at 16, the walk
slots at 32, the heads, the directory), the effects list (`+0xb0`, read from the records after `OBJ `, 28 bytes
each) and the sound at `+0xd4`, made again where it was set. The header's count is read over the global
`DAT_008791ac` and then counted up once a record, so a park loaded and saved holds twice its scripts there (28
over fourteen records), and nothing walks by it.

Measured over the six files' 374 records (`q241i/rsse.py`, `rsse-census.txt`): the records fall by handle, the
newest first; every object's `mRideScriptHandle` names a record that holds the object's id at `+0xac` and its
`MeshInstanceID` at `+0xc8`, 360 of 360; of the other fourteen records six share a thing and its model with
that thing's own script and eight have no thing. `+0x0c`, `+0x10` and `+0x14` hold another script's handle on a
few records, in pairs (Mumbo's 122 and 123 name each other, the mine cart's 115 names 116): the links between a
script and one it started, not read in the listing. `+0x60` is the count in limbo and
`+0x6c` the count bouncing with `+0x6e` beside it (`0x80006` on a Belly Bounce with six aboard).

**Who is on a thing is in both halves, and a file agrees with itself** (`q241i/riders.py`, the played jungle and
fantasy parks). Every queue runs from the object's `mFirstInQ` down the guests' `mQNext`, each guest's `mQPrev`
the one before and `mMajorDest` the object: 112 guests in 26 queues, no fault; a queue of one has no link. The
queuers are in states 11, 12, 13, 14 and 8 (a spot animation in the queue). `mPersonBeingLoaded` is set on seven
objects and names the head of the queue each time. A rider is state 16, and **every handle in a script's tables
is a guest riding that script's thing**: the heap below the heap index, a limbo slot, a bounce slot, a walk slot
in use, a head slot; 37 of the jungle park's 82 riders are in one or more. Twelve more, the eleven in the Small
Toilets and one on item 1307 (`puzzle`), have their id in one of the script's variables. The other 33 are on
the go-karts (a track ride), the mine cart (a coaster) and the tour ride, in no table and no variable of the
script: where those are held (`TRAK`, `COAS`, the tour's record) was not looked for.

**Measured in the original under Proton** (`q241i/`, two files made by hand from the shipped park with `make.py`
and a control with only its camera moved; `PREDICTION.txt`). The first file: a Security Camera made, thing 43 on
(46,29), a copy of thing 18's record at the head of the thing list and of the object list, in set `0xb`, its
cell a footprint's (type 4, the parent its own packed id, `mWho` 43), a copy of 18's model record in the slot at
the cursor (handle 91) naming a new script, and a copy of 18's script record first in the module under the
header's next handle, 16, with the thing, the model handle and a `WAIT` deadline 20,000 ms past the file's clock;
a camera gone, thing 19, its three records out, its cell bare and its slot empty; the queue piece of (52,22)
moved to another empty slot with the cell's `mMeshInstance`; and the piece of (49,22) taken out, the cell left
a queue's with nought. Loaded from the Load Park screen over a running park, the original's memory read, on the
first poll after the load: `mGameTick` 755 and counting; 42 things, 14 objects, `mFirstObject` 43; thing 43 item
1413 on (46,29) with model handle 91 and no thing 19; the model table 160 present, 8 free, cursor 92, handle 91
item 1413 naming script 16, handle 92 item 17003 on (52,22), handles 111, 114 and 119 empty; fourteen scripts,
the next handle 17, script 16 on thing 43 and model 91 at word 14 with the deadline written, and no script 9;
the four cells as written. **The made script waited the wait it was given**: its deadline passed 71 sweeps, 17.6
s of game time, after script 8's, the file's own camera, where the file puts the two 17,671 ms apart, and from
there the two ran the camera's four waits in step. The frames show a camera on (46,29) and none on (40,29),
beside the control's the other way about; the queue's piece on (52,22) drawn; and **the cell (49,22) drawn
black**, with no ground under the piece it does not name, guests still queueing across it. That is what a queue
cell written naming no model is (`SAVE_PARK_QUEUE_CELL_MODEL`).

The second file: the same, but the made script's struct is the loader's own (the fields above and nothing else,
the counter at word 0) and its model record declares no nodes and no lookup records, with one idle channel (role
12). It loaded the same; the script read word 0 on the first poll, waited 1.4 s at word 2 and joined the cycle,
and the camera stands in the frame. **So a made model record need not hold the model's node and lookup tables,
and a made script record need hold no more than the loader sets.**

**Two predictions wrong, mine:** the made script after its wait (word 5 on about 1.5 s, not word 14 again: the
loop is four waits), and the from-scratch one's stop at word 2. **Not measured:** a thing with riders or a queue
made or gone; a thing whose model has emitters (`FUN_004368f0`, reached under flag `0x200` for a type whose
`+4` has bit `0x40000`) or whose script has effect records; an object record made from nothing (the made one
is a copy of another camera's); an object control's count (one camera in and one out leaves it 2); a freed
thing id used again; what frees a slot and where it leaves the cursor (the file's cursor was set to the lowest
empty slot and read back so). **OpenTPW was not run**: `ParkWorld` was not asked to read the three files.

### OpenTPW's writer, the objects

The fourth stage (Q252), for the things the file holds; a thing bought or sold is the fifth's ("OpenTPW's
writer, a thing bought and a thing sold"). Every
record is written over where it lies: an object's is its fixed 1,099 bytes, a script's tables keep their lengths
and a model's channels their count, so nothing moves, and they go in before the people.

- **The clock moves on** (`ParkRides.Written`, `ParkClock.Put`). The load here puts the file's moment on this
  park's own clock and moves every saved reading by its distance from it ("moved, not copied", `ParkRides.Moved`);
  the writer turns it round. A moment on this park's clock goes out as the file's reading plus the time since the
  load, the clock module's first dword as the reading of now and its second moved by as much, so every deadline
  and stamp keeps its distance from the save's moment, as it does in a file of the original's. The readings a
  script keeps in its variables are turned the same way (`RideScript.Written`).
- **A script** (`RideScript.Written`, `ParkScriptStates.Put`), under the handle the scheduler runs it on: the
  counter, both stack indices and the stack, the result, the variables, the five clock and animation fields,
  limbo with its count, the bounce slots with the two words beside them and the node base, the walk slots and
  the head table; and in the header the scheduler's tick and the next handle. The record's other bytes stay the
  file's: the links to other scripts, the name's offset, the speed word, the play rate, the scream's handle, the
  body, the strings and the effects. A slot let go keeps what it held but its guest and its state, as the
  engine's does (FileFormats `saves.md`, "A bounce slot"). A walk slot's facing (`+0x14`) is not kept here and
  stays the file's. **Counted:** a script the file holds no record for (`SAVE_PARK_SCRIPT_MADE_SINCE_THE_LOAD`:
  a thing bought, a child spawned, and the ferry's and the seaplane's, which a load here makes, two a save), and
  a record whose script has ended (`SAVE_PARK_SCRIPT_ENDED`), left the file's.
- **A model** (`ParkRides.Written`, `ParkThingStates.Put`), into the record that names its thing's script: each
  channel as the engine keeps one. A running channel's clip time and third stamp are the save's moment and its
  start where it began; a held one's clip time is a whole clip past its start (`AnimTimeControl.HeldTime`). Of
  the flag word the loop, frozen, held, `0x10` and `0x20` bits are the running channel's and the rest the
  file's, because a channel here does not keep the keep-shown request (`0x8`): counted where the clip is no
  longer the file's (`SAVE_PARK_CHANNEL_KEEP_SHOWN_BIT`). **A channel held since before the load keeps the file's
  own word and clip time**: the engine's `0x10` on a held channel comes and goes (FileFormats `saves.md`, "Bit
  `0x10`"), where a channel here keeps it for good. With nothing queued the last queue's leftovers are the
  file's. The hoarding's seven bits and its progress are the thing's as they stand. **Not written, counted:** the
  node flag words and the lookup records' attached handles, which hold a head `ADDHEAD` hung
  (`SAVE_PARK_HEAD_ON_A_MODEL_NODE`; no thing of the shipped park's hangs one).
- **An object** (`ParkState.WrittenObjects`, `ParkWorld.PutObjects`): its door (`mCanLoad`), the member assigned
  and the tick they were, the queue's head, back cell and size, the guest being loaded, the six day rings and
  the two counts, the operating three, the goods' cost, quality, chance and ingredient, the price, the two
  repair floats, the service request and the two totals. The back cell and size are the file's until the queue
  is edited, and measured off the map from then (`ParkRideChoice.QueueCellsFor`). The rest of the record stays
  the file's. An object sold is left the file's and counted (`SAVE_PARK_OBJECT_SOLD`).
- **Who is on a thing is written in both halves** (`ParkPeople.Written`): the guest as they are, and a rider with
  no sprite on a thing that keeps none (`mFlags` `0x20`), as admission leaves them.
- **OpenTPW's load reads the other half back.** `ParkScriptStates` takes limbo, the bounce slots and the walk
  slots with their counts, and `RideScript.RestoreRiders` puts them back with each reading moved; `ParkState`
  takes `mPersonBeingLoaded`; and a person with no sprite holds slot nought, which is nobody's
  (`ParkGuestSprites.Taken`): a file with two such people, two members of staff resting or two riders in a
  shop, killed the load before.
- **A bounce node is numbered as the engine numbers it**, the slot's index plus the base the loader sets to 1
  (`0x00558c45`), where it was counted from nought; `ParkPeople.BounceNodeName` names id 1 `body`.

**Measured (Q252, `q252/`).** Lost Kingdom left alone from easymode's 755 until a guest rode the Belly Bounce
with others queueing behind, past the first load of arrivals; `savepark Q252` under `pause` at `mGameTick` 1344.
`rides` read script 3 at word 100 with guest 36 on node 1, and `peeps` seven guests queueing for thing 13. A
Python reader of the file found: the clock 146,165 ms past the first file's (1344 less 755 sweeps of 248 ms is
146,072) and the second stopwatch moved by as much; the script header's tick 10,770 and next handle 18; script
3's record at word 100, its bounce slot 0 guest 36 on node 1, due 22,839 ms after the clock and begun 7,161 ms
before it, 30,000 apart, the word at `+0x6c` 1 and `+0x6e` 8; guest 36 in state 16 with `mMajorDest` 13 and no
queue link; thing 13's `mFirstInQ` 35, the head of a chain of seven down `mQNext` with every `mQPrev` and
`mMajorDest` agreeing, no fault in either half; the Belly Bounce's model record on role 5, looping, begun 967 ms
before the clock with its clip time and third stamp the clock's; hoarding `0x8` on the records of things 13,
14, 16, 21, 22 and 23 and nought on the other eight; the gates' held channel `0x4` and the bin's `0xc`, the
file's own. An earlier run saved with a guest called forward wrote `mPersonBeingLoaded` 36 beside `mFirstInQ`
36, state 13. **Loaded from OpenTPW's Load Park screen**, the log read "thing 13 (script 3 at word 100) holds
1 guests in its limbo, bounce and walk slots"; a sweep on, `rides` read guest 36 on node 1 and the script a
turn further, at word 46 on a fresh `WAIT 500`; all eight guests were on the thing as they had been; and left
to run, guest 36 came off 23.5 s on. **The build before** (`9ee7fc2`), saved with one riding and seven queueing,
wrote the first file's script 3 (word 46, nobody aboard) under the first file's clock and counted
`SAVE_PARK_GUEST_ON_A_THING` eight times; after its load nobody rode and nobody queued.

**The original under Proton** took the file twice. Entering the park loads the newest file in the folder
("Entering a park"), and 27 sweeps on its memory read guest 36 still in slot 0 and three of the file's queuers,
35, 33 and 34, aboard on nodes 2 to 4. Loaded again from its Load Park screen and polled every 50 ms: on the
first poll `mGameTick` 1344, script 3 at word 100, slot 0 guest 36 on node 1, `+0x6c` 1, guest 36 in state 16
and the queuers in 11; guest 35 went 11, 13, 14, 16 and into slot 1 five sweeps on, 33 into slot 2 fourteen
on, then 34 and 29; and guest 36 left slot 0 for state 15 on `mGameTick` 1440, 96 sweeps after the load, where
their due is 92. The first frame after the load shows a guest on the Belly Bounce and the queue in its fence
(`q252/sheet-ours-beside-original.png`).

**A rider overstays in the original, whoever wrote the file.** The file of an earlier run put its rider 5,107 ms
from their due, and after the load from the list they rode 57 sweeps, 9 s over. As a control the original saved
its own park with three aboard from its Save Park screen and loaded it back: one rider came off on the sweep of
their due, two came off 32 sweeps late each, and one who boarded after the load rode 154 sweeps for a ride of
121. `UNBOUNCE` lets a rider go only in the first fifth of each second aboard (`BounceWindow`), and the script's
passes come round about once a second, so a rider just outside the window waits for the passes to drift into
it. **The original's own file beside ours** (`q252/orig/ctl-written-by-the-original.TPWS`): the same fields in
use; its riders stand where ours do; its held channels read `0x4` where it had loaded `0x14` from ours, and its
model records `0x8` on the six things a guest may be offered where ours held the first file's nought, which is
why a held channel now keeps the file's word and the hoarding goes out as it stands. Its bounce riders' sprites
are on program 66, picture set 2, where OpenTPW's rider stands (program 90) and is written so (Q52's note); its
Belly Bounce loops clip 1 at speed 1.1 where ours loops clip 0 at 1.0 (Q155).

**Predictions wrong, mine:** the script's counter at the save is 46 (it stands on 46 or on 100, its two turn
ends); a wait reads nought to 1000 (one stands up to a sweep overdue: -89 and -120 were written); the next
handle 16 (18: the ferry's and the seaplane's scripts took 16 and 17); today's takings above nought (the Belly
Bounce's price is nought in Easymode); the loop's role 2 (5 with riders); a rider off within 1.5 s of their due
in the original (above); and the same word after OpenTPW's load (the census comes a sweep after it). **Found
by the first run, which it killed:** two members of staff resting at the save, each written with no sprite, as
the original writes them, and OpenTPW's load keyed its people by sprite slot. That run's file, loaded by the
final build: "thing 14 (script 4 at word 216) holds 1 guests" and "thing 13 (script 3 at word 46) holds 1
guests", `rides` the Jungle Spray's walk slot 0 carrying guest 50 and guest 51 on the Belly Bounce's node 1,
`staff` two resting; 40 s on the walker and the rider were off and the next of the queue was riding. **Seen and not this item's:**
OpenTPW's Belly Bounce carries one rider at a time with fifteen queueing, where the original boarded five from
the same file in twelve seconds (Q222's note). **Not run in the original:** a guest in a walk slot at the save (OpenTPW's own load of one is above). **Not
run in either game, tested only:** a guest in limbo (no script of the shipped park declares any); a kept
reading in a variable; a hoarding raised; a clip queued; an object sold; a queue edited; a
head on a node; a file whose clock will not read. `docs/exe/addresses.md` not regenerated.

### OpenTPW's writer, a thing bought and a thing sold

The fifth stage (Q253). An object the park holds and the file does not is written whole, its three records made;
one the file holds and the park does not is taken out. Both lengths change, so the scripts and the models are
built again from the back of the file forwards and the world goes out with the people.

**What the game leaves in a record it has just made** was measured first: the original bought a Crazy Ape on
(41,22), saved, sold it and saved again (`q253/orig/bought-by-the-original.TPWS`, `sold-by-the-original.TPWS`),
and the object constructor `FUN_004db090` was read beside the two files.

- **The object.** The constructor joins set `0xb` (`FUN_0040f9e0`), writes `mTopLeft`, the entry and the exit as
  the anchor cell plus a pair of the item's description each, turned by the angle, and the three floats `+0x44`,
  `+0x48` and `+0x4c` as 100.0; builds `mFlags` from the description; sets `mState` nought for a thing a guest may
  be offered and 3 for any other (`0x004db4fb`, `FUN_004e0e60`); pushes the item's starting speed into the script
  and keeps it; stamps the date from the park's calendar; adds one to its item's object control and stamps the
  control with `mGameTick` where it reads nought (`FUN_004d3d10`); copies the two lines of its name
  (`FUN_00413020` into `+0x3c8`, `FUN_004130b0` into `+0x40a`); and closes a thing with a queue (`mCanLoad`
  nought, script variable 6, the hoardings raised). In seven park files `mX` and `mY` hold `0x80` in the low
  byte, `mState` is 0 or 3 by that rule and the first float reads 100.0 on all 349 placed objects, and `mTopLeft`
  is the anchor's id on 345 (the two items with a pair that is not nought are the Huge Hollow Rock, 1427, and
  the Dino Karts Tunnel, 1501).
- **The name** is two rows of the running language's `OBJECT_NAMES.str`. `FUN_004147f0` and `FUN_00414870` walk a
  table in the executable, twelve bytes an item (`0x007488d8`: the item's id, the first line's row, the second's),
  281 items; a ride's name is two rows and any other's one, its second the empty row 1. Each line is copied to 32
  characters. The fresh record's bytes past each terminator are unwritten memory.
- **The cells.** All sixteen cells of the ape's footprint hold type 4 (9 on the entrance, 10 on the exit), the
  anchor's packed id as their parent and tile `(0, 8, 0)`; only the anchor names the thing in `mWho`. The sale
  left all sixteen bare on tile `(0, 55, 0)`. Every footprint cell of seven park files is on tile 8 and every
  bare cell on 55. The entrance holds one link, `0x01`, to the queue cell the placer laid over the path before
  it, and that cell `mMeshInstance` 92, its own model.
- **The script and the model.** The fresh script record is first in its module under the header's next handle,
  with `-1` at `+0x98` (369 of 374 records in six files), the object's speed 60 as its speed word and its
  directory `data\levels\jungle\Rides\monkey\`. The model took the slot at the cursor, 90, and the queue piece
  91; the sale emptied both and left the cursor on 90, the lowest empty slot. The sale also took the item's count
  to nought and left its stamp.

**What OpenTPW writes.**

- **A made object** (`ParkWorld.MadeObjectRecord`): the record as the constructor and the serialiser leave one.
  `mTopLeft` is the anchor's id, which is wrong for an item whose description holds the pair (not read here; two
  items of Lost Kingdom's). The bytes past a name's terminator are nought. The date is the park's own stamp of
  the purchase. Its queue's back cell and size are read off the map (`ParkState.WrittenObjects`).
- **The lists** (`ParkWorld.PutPeople`): the made go into the thing list newest first, people and objects by id
  together, and to the head of the object list; the gone are left out of both and the list closes over them. A
  made object stands on its anchor cell's chain behind whoever stands there, and a gone one's cell is headed
  anew. Set `0xb` takes the made and every set loses the gone.
- **The cells** (`Level.WrittenCells`): a cell that has joined the footprint of a thing written, or left the
  footprint of a thing taken out, is written, on tile 8 under a footprint, which the park here does not keep;
  a cell left bare is on tile 55 already, as a sale here leaves it. A footprint of a thing that is not written
  is still counted and left
  (`SAVE_PARK_FOOTPRINT_CELL`).
- **A made script** (`RideScript.Made`, `ParkScriptStates.MadeRecord`, `Splice`): the struct as the loader
  `FUN_005587f0` fills it with the running state laid over, the addresses nought, the body and the string blob
  as the `.RSE` file holds them, and its tables. The speed word is the object's operating speed, or the
  loader's 50 where that is nought. The directory is OpenTPW's own path, in lower case (`rides`). A walk slot's
  facing is nought (`SAVE_PARK_WALK_SLOT_FACING` where one is in use) and no started effect is written. A gone
  thing's records, its own and any its script started, are taken out, and the count the records are walked by
  is kept.
- **A made model** (`ParkThingStates.Plan`, `MadeRecord`, `Splice`): the slot at the cursor, the lowest empty
  one once the gone are let go, the older of two made taking the lower; the item, the `mTopLeft` cell, the item's
  footprint, the placer's flags `0x32f`, the script, the hoarding as it stands, the angle and the channels as
  they run. **It declares no node words and no lookup records**, which the reader answers by keeping the fresh
  model's own (above), so a head hung on a node and a node a script has hidden are not in it
  (`SAVE_PARK_HEAD_ON_A_MODEL_NODE`), and a running channel's keep-shown bit is not known
  (`SAVE_PARK_CHANNEL_KEEP_SHOWN_BIT`). The header's three counts are kept.
- **The controls** (`ParkWorld.PutControls`): every item's standing count and first-build stamp as
  `ParkState.BuiltItems` runs them.
- **Not written, counted, and left as the file has them:** a track ride, whose record in the track-rides module
  is not made or taken out, and a thing whose folder holds an emitter (`SAVE_PARK_OBJECT_BOUGHT`,
  `SAVE_PARK_OBJECT_SOLD`); with them stay their cells. A queue cell's model is Q254's.
- **A bought thing's state of repair and remaining life start at 100** in the running park too
  (`ParkBuilding.Constructed`): they started at nought, so a bought toilet was dirty before anyone had used it.

**Measured (Q253, `q253/`).** Lost Kingdom from `easymode.TPWI`: `buy 1101 41 22 0` (a Crazy Ape, thing 43, on
the cell the original built its own on), `buy 1413 46 27 0` (a Security Camera, thing 44), `sell 16` (the Drinks
Shop), `savepark Q253` under `pause` at `mGameTick` 848. `rides` read script 18 at word 26 with no wait for the ape
and script 19 at word 8 with 384 ms to wait for the camera. A Python reader of the file found 43 things headed 44,
43, 42; fifteen objects, the chain 44, 43, 15, 24 and on; **thing 43's record the same as the original's own
bought ape's in every byte** outside the list link, the date, the name's tail, the script handle (18 for 16: the
ferry's and the seaplane's scripts take 16 and 17 at a load here), the rings, and the queue's back cell and size
(below); the names `Crazy` and `Ape`; **the script's record the original's own in every dword of the struct** but
the addresses and the handle, with the same body, strings, variables and head table (its stack's twenty slots,
none of them live, are nought here and mostly `0xFFFFFFFF` there, and its folder reads `rides` for `Rides`);
model handle 91 item 1101 on (41,22), four by four, flags `0x32f`, script 18, hoarding `0x9` at 1.0, no tables, one
channel on role 0 held; the camera in handle 92; no thing 16, no script 6, handle 116 empty, the header 162, 6, 92;
set `0xb` without 16 and with 43 and 44; the shop's four cells bare on tile `(0, 55, 0)`; the ape's sixteen cells
the same as the original's file's in model, parent, tile, type and `mWho`; the controls 1203 at 0 with its stamp
15, 1101 at 1 stamped 778, 1413 at 3. **OpenTPW's own load** of the file read fifteen things in `rides`, 43 on
script 18 at word 26, 44 on script 19, no 16, and both still there 82 sweeps on.

**The original under Proton**, the file loaded from its Load Park screen and its memory polled every 50 ms: on the
first poll `mGameTick` 848; 43 things and `mFirstObject` 44; thing 43 item 1101 on (41,22) on model handle 91 and
thing 44 item 1413 on (46,27) on handle 92; no thing 16; the model table 162 present, 6 empty, cursor 92, handle
91 naming script 18 and 92 script 19; script 18 on thing 43 and model 91 at word 26 with no wait; and **script 19
on thing 44 at word 8 with its deadline 384 ms past the file's clock, the wait written**. Its counter moved to
word 11 between 0.51 and 0.56 s after the load, two sweeps on, and it went on through the camera's four waits,
1,669, 5,138, 1,579 and 10,031 ms, as the file's own camera did beside it (1,669, 5,089, 1,552, 10,028). 178
sweeps on both things and both scripts were as they had been. The frame after the load shows the ape under its
hoardings, the camera's pole and the litter bin with no shop beside it, as OpenTPW's frames at the save and after
its own load do (`q253/sheet-ours-beside-original.png`).

**OpenTPW's placer parts from the original where a queue's first cell is laid over a path**, found by the file's
three differing bytes: the ape's entrance cell (42,22) holds links `0x82` here and `0x01` in the original's file.
Clearing the path under the stub takes the entrance's link to it, and the path cells either side then link to
the entrance diagonally. With no link the queue is not measured, so `mBackOfQueue` and `mQueueSizeInCells` go out
nought where the original's hold the stub's cell and 1 (Q256). A camera bought here has `mFlags` nought where the
file's own hold `0x10`, a bit of the description not read (`BOUGHT_OBJECT_FLAG_BITS`).

**Predictions wrong, mine:** the queue's back cell and size (above: three bytes, restated after the first run);
and a control's stamp one tick after the tick read before the command (two). **Not predicted:** the camera at
zoom 50 from the north stood inside the ape's head in both games; the frames are from the south at 110. **Found
by the bugs put back:** a bought thing's queue is read off the map already (the placer's rewalk marks it), a sold
thing's cells are on bare ground's tile already, and no name is longer than a line: three lines of this stage
that did nothing were taken out. **Not run in either game, tested only:** a thing sold whose slot a thing bought
then takes, two things bought on one cell's chain with a person, a made script with riders or a walker, a table
with no empty slot, a track ride or an emitter bought or sold (counted and left), a thing bought and sold again
before the save, a save with no people handed over (refused). **Not run in the original:** a made thing with a
guest queueing or riding (the ape was shut), a made thing turned, a made shop. **In the original the things, the
slots and the scripts are its memory's** (`orig/look.py`); the two things in its frame are told from the file's
own by where they stand, by eye. `docs/exe/addresses.md` not regenerated.

### OpenTPW's writer, a queue cell's model

`Level.WrittenCells` hands the writer every cell whose queue piece is not the file's (`ParkFileWriter.QueuePiece`):
a cell that has joined the queue, left it, or changed its tile's index or angle. `ParkFileWriter.Body` does for
each what the retile `FUN_005365d0` does: the slot the file's cell names is emptied, and a cell still in the queue
is given a record in an empty slot (`ParkThingStates.QueuePieceRecord`) and names it in its `mMeshInstance`
(`ParkWorld.PutCellModels`); a cell that has left names none.

- **The record is the original's, byte for byte** (FileFormats `saves.md`, "A queue piece's record"): item 17000
  plus the tile's index, the cell, one cell square, flags `0x33a`, no script, 360 less the tile's angle, its node
  words nought (two of them; four for the end and the two bins) and one idle channel. Every queue piece of the
  nine park files of the original's to hand is the bytes `QueuePieceRecord` makes for its cell, 225 of 225
  (`q254/pieces.py`: the shipped park's four, 208 in four played saves, none in a fifth, and 13 in three saved under Proton), the
  original's own dead end among them (index 1, the piece of a queue that has not reached a path:
  `q253/orig/bought-by-the-original.TPWS`, handle 92). No file holds index 0, which names the same model.
- **A deviation: the slots are dealt as the file is written**, not as the things were made. The original gives
  each model the lowest empty slot at its making, so its table holds the order the player built in. Here the
  objects bought take the lowest empty slots first, the oldest first, then the queue cells in the map's order.
  Every handle names its own record, which is all a load reads. The original's bought ape took handle 91 and its
  queue piece 92, as here.
- **A handle that names no queue piece's record is not freed**: a cell whose `mMeshInstance` names an object's
  model, or a slot past the table, is written naming none and the slot is left.
- **Still counted** (`SAVE_PARK_QUEUE_CELL_MODEL`): a cell on a tile outside the eight pieces, which draws
  nothing here either (`QUEUE_TILE_INDEX_OUTSIDE_TABLE`), and every changed queue cell of a park written with
  its things as the file's.

**Measured (Q254, `q254/`).** Lost Kingdom from `easymode.TPWI`, held with `pause`. The Belly Bounce's queue runs
(52,22), (51,22), (50,22), (49,22) to the path. `delqueue 49 22` and `delqueue 50 22` cut two cells; the queue
tool (`tool queue 13`, then `worldclick` on (51,22), (50,22), (50,23) and (48,23)) laid (50,22) again, (50,23)
and (49,23) and joined the path at (48,23); `savepark Q254`. The log at the save: (49,22) gives up model 114 and
names none; (50,22) gives up 113 and names 91, item 17003 turned 0; (49,23) names 92, item 17005 turned 90;
(50,23) names 93, item 17004 turned 180; nothing counted. A Python reader of the file: the six cells' handles 0,
91, 112, 111, 92 and 93, the module's header 162 present, 6 empty, cursor 93, slots 113 and 114 empty, and the
three records as above (`PREDICTION.txt`, P2 and P3). OpenTPW's own load of the file draws five pieces on the
tiles written. **The original under Proton**, the file loaded from its Load Park screen: its memory on the first
poll after the load reads the same header, handles 91, 92 and 93 as items 17003, 17005 and 17004 on their cells,
113 and 114 empty, and the six cells' handles as written; its frame shows the fence from the torches at the path
along (49,23), round (50,23) and (50,22) to the ride, grass on (49,22) and no black cell
(`q254/sheet-ours-beside-original.png`).

**The control, the build before**, the same clicks: four cells counted; the file's (49,23) and (50,23) name no
model, (50,22) still names 113, a straight's record, and (49,22), bare ground, still names 114. The original
drew (49,23) and (50,23) black, a straight on (50,22) where the bend belongs and the old end piece with its
torches on the bare cell (49,22). **Not predicted, and not decoded:** in its memory after that load the cells
(49,22) and (50,22) read no handle, though the file gives them 114 and 113 and both slots still hold their
records and are drawn. Something in the load lets go of a cell's handle where the cell's tile and the record
disagree; it was not looked for.

**One prediction wrong, mine:** six pieces drawn; five stand (four, less two, and three more). The cells' tiles
in the first prediction were seen in a trial of the same clicks before it was written; the handles, the file and
both loads were not. **Not run in either game, tested only:** a cell under a thing bought, a cell a thing sold
has left for a queue, a handle that names another thing's model, a piece written with no people handed over, a
piece beside a thing bought in one file (the tests' ape and its stub: 91 and 92). **Not run in the original:**
a dead end's record written by OpenTPW (the run's queue was joined), a bin's. The edits were the console's
`delqueue`, `tool` and `worldclick`, the save `savepark` under `pause`: no player's hand reaches the writer yet.
`docs/exe/addresses.md` not regenerated.

### Read, not run

The objects' other rules above are the ten files' and the listing's.
**Not run in either game:** a guest gone (tested; one run met one), a table grown past its hundred slots, a
person written on an object's cell, a member of staff resting or in the hand at the save, a guard hired. **Not read:** what `mLastRecordedMapId`, `mNextServiceInterval` and the sprite
record's words past `+0xcc` hold, which a made record leaves at nought and the original walked on with. The
readers of the sound module, the advisor scoring and the UI block were not read, only their writers. What
`VANT`'s clock is for, what the action recording is read by after a load, and `FUN_005408f0` are not traced. The
fresh world's save was not looked at. `addresses.md` is not regenerated.

**The harness** is `q241e/`: `ghidra/` (the dumps of every module's writer and reader), `roundtrip/` (the .NET
container), `patch.py` (one change to a body), `census.py <park file>...` (the ties above, read-only),
`PREDICTION.txt`, and `orig/` (the four logs, the frames `l1`, `l3`, `h2`, `c1`, `PREDICTION-result.txt`).
Q241f's is `q241f/`: `confirm.py` (the write and the load, with its own reader of the file), `PREDICTION.txt`
(the three runs and their results), `mutate.py`, `orig/` (`read.py`, the three load logs, the frames) and the
sheet of OpenTPW's frames beside the original's. Q241g's is `q241g/`: `cells.py` (a file's cells by type: the
fields no reader here holds), `confirm.py`, `PREDICTION.txt`, `mutate.py`, `orig/` (`cellread.py`, the original's
cells from memory; the logs before and after the load; the frames) and the sheet. Q241h's is `q241h/`: `pk.py`
(a park file taken apart in Python), `chains.py` and `fields.py` (the ten files' chains, staff lists and unnamed
fields), `ghidra/` (the constructors and serialisers read), `confirm.py`, `PREDICTION.txt` (every run and its
result), `mutate.py`, and `orig/` (`things.py`, the original's node table from memory; the logs at the load, 30 s
and 60 s on; the frames; `first/`, the first run's). Q250's is `q250/`: `confirm.py`, `PREDICTION.txt`,
`mutate.py`, and `orig/` (`pool.py`, the original's pool and arrival block from memory, once or polled;
`a-load.log`, the poll across the load; the hire screen's frames `h1` and `h2`). Q241i's is `q241i/`: `rsys.py` and `rsse.py` (the model
and script modules walked record by record; `rsse-census.txt`), `riders.py` (who is on a thing, both halves),
`make.py` (the hand-made files), `ghidra/` (the loader, the retile and the object serialiser), `PREDICTION.txt`,
and `orig/` (`look.py`, the original's objects, model slots and scripts from memory, once or polled; `a-load.log`
and `b-load.log`, the polls across the two loads; the frames `e1`, `e2`, `f2`) with the sheet of the three. Q253's is `q253/`: `look.py` (a made object's three records, its cells and its control),
`orig/bought-by-the-original.TPWS` and `sold-by-the-original.TPWS` (the original's own saves of a Crazy Ape bought
and sold), `004db090.c` (the object constructor), `gen_names.py` (the name rows out of the executable),
`confirm.py`, `loadonly.py`, `PREDICTION.txt`, `mutate.py`, and `orig/` (`go.sh`, `look.py`, the polls
`g-load.log` and `r2-load.log`, the frames) with the sheet.

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
| The inflated body: the World block, and what each thing and script was doing, read and written over | `OpenTPW.Files/Formats/Save/ParkWorld.cs` (`.People.cs`, `.Pool.cs`, `.Objects.cs`), `ParkThingStates.cs`, `ParkScriptStates.cs`, `ParkClock.cs` |

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
