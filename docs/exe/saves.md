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

The compression routine `FUN_005f8050` has not been identified.

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

**Q207, failed selection reload.** A null `LoadPlayer` result revokes `CanWrite` on the cached roster profile,
retaining its readable progress. Immediate key saves and deselection both remain blocked even if a valid file
returns. Only a successful reload replaces that instance and may restore write permission. The regression scans
7 keys, removes the file during selection, restores 19, and verifies unchanged bytes through both save paths;
reloading then permits saving 20. Restoring the original selection code fails this regression. This closes an
inherited gap in the preservation policy; it is not a native-game behavior claim.

The same 7→failed selection→restored 19 sequence passed through real lobby controls with disposable saves.
`runtime-profile/preserved-result.json` records the byte-identical restored profile after the confirmation tick;
`successful-reload-nineteen.png` shows the reloaded keys. The normal second deselection writes successfully.
Evidence is in the Q207 directory named in `park-engine.md`; installed saves remain hash-identical.

## Unresolved

- The park body's compression (`FUN_005f8050`).
- What "tcf" stands for.
- The theme list's own names.
