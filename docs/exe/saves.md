# Saves: `Config.tcf`, `gms.dat`, `.TPWS`

The byte layouts on this page are duplicated in the FileFormats clone (`formats/options-and-players.md` on its
`docs/save-files` branch, `formats/saves.md` on `docs/sam-and-saves-corrections`), which is the authority for bytes.
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
`addrbook.dat`, `outbox.dat`, `gms.dat`, then `Config.tcf` — in that order. `gms.dat` is also rewritten after every
park save.

**Delete (`b_dellog`)** shows message box text UITEXT 399 — empty in the shipped files — then recursively deletes the
player directory. The slot is emptied and nothing else is written.

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
| — | 2×M | ride ids | M words |

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
a magic number — reading it as four bytes of "F4 01 00 00 magic" is what hid the distinction, and the FileFormats
`saves.md` still reads it that way on `master` (corrected on its `docs/sam-and-saves-corrections` branch). `SaveReader`
accepts both and reports any other number it finds; `ParkSaveTests.TheShippedParkIsVersion400` pins that.

### Preamble

Measured from `data/levels/jungle/Easymode.TPWI`, the one file of this shape that ships. A fixed 1549-byte preamble
precedes the compressed block.

| Offset | Size | Field | What it is |
|---|---|---|---|
| `0x0000` | 4 | version | 400 shipped, 500 saved |
| `0x0004` | 1 | padding | zero |
| `0x0005`–`0x033C` | 824 | copyright notice | EA legal text, UTF-16 — 824 **bytes**, so 412 characters |
| `0x033D`–`0x0603` | 711 | padding | all zero |
| `0x0604` | 4 | file type | `00 01 22 19` |
| `0x0608` | 1 | file version | 133 (`0x85`) |
| `0x0609` | 1 | online flag | 0 = offline save, 1 = `upload.LAYS` |
| `0x060A` | 3 | padding | zero in the shipped park (`0x060A`–`0x060C`); where the online block's start sits against it is not settled |
| — | var | online block | `0x060C`–`0x0846`, **only** when the online flag is set; contents unknown |
| `0x060D` | 4 | tag | `BILZ` |
| `0x0611` | 4 | inflated size | what the payload expands to |
| `0x0615` | 4 | block size | the whole block, tag and header included |
| `0x0619` | 16 | unknown | 15, 9, 0, 0 in the shipped park |
| `0x0629` | var | ZLIB stream | runs to the end of the file |

The 28-byte block header counts the tag, so it is 4 + 24, not 4 + 28.

The body inflates to modules, each **followed** by a four-character marker (`WRLD`, `PART`, `CLOK`, ...).

**Divergence, unresolved.** The Ghidra trace described the preamble as a language byte, `0x500` bytes of UTF-16 EA
legal text, a `0x100`-byte block, then two dwords with an optional author block. The byte counts above are what the
shipped file actually measures and disagree with that reading; which fields the trace was naming — in particular
whether byte `0x0004` is a language byte and where an author block would sit — is not settled.

**No `.TPWS` has ever been read by this project.** The only file of this shape available locally is
`Easymode.TPWI`, a shipped Instant Action starter. Everything above is measured from that one file or traced from the
executable, so the reader must **not** be assumed to generalise to a real saved park — the version difference is
already one proven case where it does not.

The compression routine `FUN_005f8050` has not been identified.

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

**Not verified against files written by the real game** — no such files exist locally.

Caution: a run of the game writes into the real installation's `save/` (`Config.tcf`, `opentpw.cfg`, player folders).
Never empty it: delete only what that run created (`CLAUDE.md` rule 12).

## Unresolved

- The park body's compression (`FUN_005f8050`).
- What "tcf" stands for.
- The theme list's own names.
- The preamble divergence above.
