# Boot sequence

How the original game boots: `WinMain` takes a single-instance lock, builds the players, loads the flag word and `config.tcf`, then enters a message loop whose idle pass runs a four-step one-time init (language tables, the disc check, the window and D3D device, `Boot_Init`) followed by `Game_StateMachine` on every pass. Boot init picks texture folders, parses options and settings, seeds the random number generator, starts sound, and holds a loading screen while the UI loads. The state machine then walks the two intro movies, the front-end load, the lobby loop, the park load and the in-park loop, and back out again; a single bit of the flag word (0x200) decides whether the front end exists at all. Traced in `/testme.exe` with headless Ghidra. The named functions below (`Boot_Init`, `Game_StateMachine` and the rest) were named in the Ghidra project at the same time; every bare address is still an unidentified `FUN_<address>` there. OpenTPW does not follow this sequence yet — it goes straight to the lobby behind the bar loading screen. What it skips on the way is counted once a run in `Game.Run`, before the first lobby: `BOOT_SPLASH` and `BOOT_LEGAL_SCREEN` (`ui.md`, "Loading screen"), and `INTRO_MOVIE_BULLFROG` and `INTRO_MOVIE_PARK` (states 5 and 7; "The movie player" below). The front end's flag 0x200 is always set here, so the movies are always reached, and all nine `.tgq` ship in `Data\Movies`. The lobby plan cut these (`docs/QUEUE.md`, section G).

## WinMain — `WinMain_Main` 0x0045a960

1. 0x0045f640 takes a `CreateMutexA` lock, so only one copy runs.
2. Setup: creates players (`Players_Construct`); sets the flag word (`Flags_LoadDefaults`); reads `save\config.tcf` (0x00424930); registers `MSWHEEL_ROLLMSG`. The install's `safemode.bat` copies `safemode.tcf`, 32 bytes, over `config.tcf`.
3. Message loop (`GetMessageA` once, `0x0045ab2a`; `PeekMessageA` here and in three other loops, `FUN_00479260`,
   `FUN_004798e0` and `FUN_005f86e0`, whose waits are not traced; Q194). When idle and running (`DAT_007a1a14` bit 1):
   - **Once**, and every step must succeed or the game quits:
     1. 0x00419710 — language and string tables and the keyboard shortcuts, from `Data\Language\`.
     2. 0x0045f7a0 — **the disc check.** `FUN_005aa500` lists the CD drives (`FUN_005f87b0`, mask 8) and asks each for its volume label (`FUN_005f89f0`, `GetVolumeInformationA`); until one reads `TPWorld` it shows a Retry/Cancel box (`MessageBoxW`, type 0x15). Cancel fails the step, so the game quits.
     3. `Window_Create` 0x0044e080 — the "Theme Park World" window and the D3D device. `DirectDrawCreate`, one of
        `DDRAW.dll`'s only two imports, is called from `FUN_00563460` (`0x00563532`, `0x00563651`, `0x00564017`; also
        its one `DirectDrawEnumerateA`, `0x00563574`) and `FUN_005fa2b0` (`0x005fa2bc`); Q194, from the bytes.
     4. `Boot_Init`.
   - **Every idle pass:** 0x005b5bf0, then `Game_StateMachine`. Its return value is ORed into `DAT_007a1a14`, where 2 means quit.
4. On quit, `Game_Shutdown` 0x005503f0 runs. It first runs state 0xb if a park is up, then tears everything down.

## The flag word — `DAT_007a1a8c`

`Flags_LoadDefaults` 0x00449df0 sets it to 0xc0e15. It is replaced only if `dialog.tcf` loads; no such file is in the install, so that is probably a developer settings dialog.

| Bit | Meaning |
|---|---|
| 0x1 | Fullscreen or windowed. |
| 0x200 | **Front end.** Set: intro movies, then the lobby. Clear: straight into state 9 (a park), and leaving the park quits. Nothing found clears it. |
| 0x800000, 0x1000000, 0x2000000 | Game types 0, 1 and 2, set by 0x00550d80, "SetGameType"; the mode object's constructor 0x00550ca0 derives the type from them and rewrites them. Type 1 runs the ONLINE CHAT init. |

## Boot init — `Boot_Init` 0x0054dcf0 (once)

1. **Textures:** 0x004688b0 picks the texture folders by a detail setting (`DAT_00785874`): `stexture`/`ssharete` or `textures`/`sharetex`.
2. **Checks and options:**
   - 0x005d55a0 checks an "Authorization Stamp" and logs the result.
   - 0x0040f000 parses options, probably the command line: `version quickload nodebug noload SAVEDEBUG flmouse bwcursor`. If it fails, the game aborts.
   - 0x0040cb80 reads the settings sections `system camera cheat coaster shortcuts`.
3. **Log and random seed:** logs "Compiled Mar 24 2000 at 15:14:05" and seeds the random number generator from `GetTickCount`.
4. **Sound:** 0x0051b660 initialises it, volumes coming from the settings. It then plays effect 0xd2 (210) from category `DAT_00803a2c`, flat (`Sound_PlayEffect( 0, cat, 0xd2, 0, 0, 0 )`). **It is silent** (Q139): the effect is one variation of one sample, `BUTTON01.mp2`, the click's own (effect 31), with the variation's volume at (0, 0) where effect 31's is (100, 100); and the original's mix, written to a file from the device's opening (`q139/orig/`, `measure.py`), is exact zeros through the whole boot, 76.8 s on the file's axis, until the first sound after it. The control was weak: the one click made in the lobby afterwards matched `BUTTON01` at 0.26 under the lobby's own sound, below the 0.4 asked for, so the reading rests on the zeros and the file's volume, not on a match. What a play at volume nought is for was not read. OpenTPW plays it at that volume (`UiSounds.Boot`). Then `Sound_ApplyGroupVolumes`.
5. **Loading screen and UI:** `LoadingScreen_Begin` (500 steps) shows the Bullfrog `splash_<lang>.tga` for at least 2.5 s, then `welcome.tga` with the `legal_<lang>.tga` copyright strip and no bar. Behind it, `UI_Init` 0x00489ca0 hides the cursor, loads the UI meshes ("Loaded %d UI meshes"), sets up the UI and calls `Dialup::Initialise`. `LoadingScreen_End` holds the screen until 3 s after it appeared.
6. **Finish:** 0x005989c0 (the advisor reset). The first state is 4 if flag 0x200 is set, else 9.

## Main state machine — `Game_StateMachine` 0x0054e360

The state is `DAT_0087906c`. The machine runs once `DAT_00879068` is set, at the end of boot init.

Its head (`0x0054e381`..`0x0054e3dc`) keeps the frame's length: `0x00875030` = the clock `0x00785970` less
`0x00875034`, in ms; after the print it stores the clock again in `0x00875034`. **The original's FPS print is off in the shipped game** (Q194; dead by
CONTENT): when `[0x007b05d0]` (the mouse-manager object `0x007b05a8` `+0x28`) is non-zero it calls `FUN_0046c0d0`, which
draws `"FPS: %4g"` (`0x0074d85c`, its only reference) of 1000 / the length at (0, 0) through `FUN_0057cb10`. The flag's
one write is 0, in the initializer `FUN_0046c0b0`; a write through a pointer is not ruled out.

| State | What happens |
|---|---|
| 4 | Goes to 5. |
| 5 | Once no key is held (ESC, Space, or the **unidentified** check 0x006595a8), `Intro_PlayBullfrogMovie` 0x0054df20 plays `Data\Movies\bf.tgq`. Then 6. |
| 6 | Waits for the movie to end (0x0051b300 returns nonzero). ESC, Space or 0x006595a8 skips it (0x0051b350 stops it). Then 7. |
| 7 | Once no key is held, `Intro_PlayParkMovie` 0x0054e070 plays one park movie, chosen by the local day of the month & 7: 0 bub, 1 buc, 2 grav, 3 jug, 4 mir, 5 plan, 6 roc, 7 roll (`Data\Movies\*.tgq`). Then 8. |
| 8 | Same as 6, then 1. |
| 1 | **Front end load**, behind the loading screen with the bar. Loads weather and shadow textures (0x00550950: snow, raindrop, lightning, alphkid); the advisor model from `%s\Global\Advisor` (0x00429ba0); particles `Data\Particle\Tp2.plb`; the lobby object (0x005d5770, `DAT_00f82884`); then `FrontEnd_Init` and 0x00540900(0,1). The load is bracketed by 0x006591e3 and 0x00659201. Then `LoadingScreen_End`, `Sound_SetSpeechDuck(0)`, then 2. |
| 2 | **Lobby loop**, while lobby object +0x14 == 1: `Advisor_Update`, sound listener, render, present, and fixed 31 ms ticks of 0x00520130 with at most 500 ms of catch-up. Otherwise 3. |
| 3 | **Leave the lobby.** 0x005d5cf0 gives the choice (2 means play a park). Frees the lobby, begins a loading screen and loads the "levels" list. Then 9, or 0xc to quit. |
| 9 | **Park load**, behind a new loading screen whose Begin resets the bar. An online-park branch comes first, keyed on `DAT_00f7d88c` +0x3f0 (0x005b50b0's object, ONLINE NEWS). Then: RSSE scripts init (0x00551600), particles, the player (a "debug" slot when there is no front end), weather textures, advisor paths (0x00457a90), 0x00407d80, the level load (0x00407e00; it loads a QuickSave first only while `DAT_00788160`, written by the options parser 0x0040f000, is set), 0x00457c30. Any failure returns 2, which quits. Offline, the park's own save then loads over the new world: the newest `*.TPW*` in the player's theme folder (`FUN_005accf0`, `0x0054f12b`; `park.md`, "Arrivals"), through `FUN_00414d40` in mode 2, which restores the saved clock (`FUN_00415140`). Game type 1 (`[0x00fb3b7c]`, `0x0054f059`) loads in mode 1 instead (`0x0054f113`): the action recording alone, no clock restore, then the layout replay (`0x0054f11f`). Game type 1 runs the online chat init, with a 5-minute timeout. `LoadingScreen_End`, then 0xf. |
| 0xf | 0x00550b30 when not online, then 10. |
| 10 | **In-park loop.** Fixed 31 ms ticks with at most 2000 ms of catch-up; see [Tick rates](#tick-rates) for what runs at which frequency. Ticks run only while the app is active or windowed. Also each pass: listener, `Advisor_Update`, render, present, `Scr%05ld.tga` screenshots and the "E W R P S" timings line. `DAT_00879088` == 1 goes to 0xd; 2 or 3 goes to 0xb. |
| 0xd | Goes to 0xe, which calls `Advisor_StopQuietly(1)` (0x005996d0) to stop the advisor and 0x005ac5f0, then goes back to 10. |
| 0xb | **Leave the park:** teardown. Then 0xc (quit) if `DAT_00879088` == 3 or there is no front end; otherwise 9 if another park is pending (+0x3f0), else 1, back to the lobby. |
| 0xc | Final teardown of the players and online objects; sets the quit bit. |

**A fresh Full Simulation park (Q197).** State 9 constructs a world even when the player has no save:
`FUN_00407d80` allocates `0x1da748` bytes and calls `FUN_00515540`; `FUN_00515660` resets the thing
allocator before `FUN_00407e00` reads the catalogue (`FUN_00413140`) and balance (`FUN_005156a0`).
The latter creates ten managers, then the unplaced gates and lights. `FUN_00407f20` loads `Scape.omp`
and sky, `FUN_004080e0` lighting, and `FUN_00457c30` the attribute map and editable terrain cells.
The later offline save search (`FUN_005accf0`) loads a save only when it found a candidate;
without one the constructed world remains. `Easy_Standard.sam` is conditional on game type 2.
See `park-engine.md`, "A fresh Full Simulation world", for the initial objects, money and map.
Lead: Aluzed's OpenTPW-decomp fork, review gap3-2 through gap3-11; checked here against the
2.0 executable and the original's jungle `restart.INTS`.

## The movie player (states 5 to 8)

What the executable does with a `.tgq`, written down for when the movies are taken up (Q195). OpenTPW plays none; it
counts `INTRO_MOVIE_BULLFROG` and `INTRO_MOVIE_PARK`. The file's bytes are FileFormats `video.md`; this is the code.
Every function below was read in Ghidra, and the dequant builder, the macroblock decoder and the stereo ADPCM decoder
were also run unchanged on the shipped movies (`docs/TOOLING.md`, "The executable's own routines under unicorn").

**Starting one.** `Intro_PlayBullfrogMovie` resolves the path and calls `FUN_0051b010( file, hwnd, x, y, 0 )`. That
refuses unless the player exists (`DAT_00802bc0`) and none is playing (`DAT_00802bbc`), fills the player's settings at
`DAT_008023f0`, creates it with `FUN_0066f4e0`, and sets its volume with `FUN_0066f520` to
`DAT_00802ba8 * 10000 / 1023 - 10000`, in hundredths of a decibel.

**The chunk reader `FUN_0066e410`.** Reads the 8-byte chunk header with `mmioRead`; the size counts the header. It
takes TGVk, TGVf, MADk, MADm, MADe, SCHl, SCDl, SCCl and SCLl in both byte orders and the rest in one, and sends a
`SEAD` chunk to `FUN_0066e920`. Video frames go to the queue (`FUN_0066e840`) with a codec type:

| FourCC | Type | Frame decoder (`FUN_00670c20`) | Shipped movies |
|---|---|---|---|
| `kVGT`/`TGVk` | 1 | `FUN_006777d0`, then `FUN_006779e0` | none |
| `fVGT`/`TGVf` | 2 | `FUN_00677310`, then `FUN_006779e0` | none |
| `pQGT`/`MUVf` | 3 | table `FUN_00671000` (into `DAT_00fb7820`), then `FUN_00671230`/`FUN_00671540`; block decoder `FUN_00672e60` | none |
| `pIQT`/`UV2f` | 4 | TQI, below | all nine |
| `MADk`, `MADm`, `MADe` (and reversed) | 5, 6, 7 | `FUN_006771a0` | none |

`SCHl`/`1SNh` go to `FUN_0066ed60`, `SCDl`/`1SNd`/`SNDC` to `FUN_0066e9a0`, `SCCl`/`SCLl` are skipped, and
`SCEl`/`1SNe`/`SEND` end the file. Types 1, 2, 3 and 5 to 7 are dead by CONTENT.

**A TQI frame (type 4).** `FUN_00670890` reads the header: width, height, the quant byte, and the flags byte's bit 1
(player `+0x40`) and bit 0 (`+0x44`). `FUN_00670c20` then builds the dequant table with
`FUN_006710c0( player + 0x408, quant )` and walks the macroblocks in raster order with `FUN_00671800`,
`FUN_006718a0` or `FUN_00671940` by output format. In `FUN_00671800` flag bit 1 picks `FUN_0067519d` over
`FUN_00675579`; every shipped frame sets it. All five per-macroblock wrappers call `FUN_006747d0`.

- **Dequant table `FUN_006710c0`.** Quant 100 gives `Q[i] = A[i] * 8`. Any other quant gives
  `f = (107.0 - quant) * 0.0625`, then `Q[i] = (A[i] * ftol( M[i] * f * 65536 )) >> 16`, rounded up when bit 15 is set.
  `Q[0]` is always `A[0] * 8` = 65,536. `A` (`0x00705c88`, 64 int32) is the AAN scale table with 8192 for 1.0. `M`
  (`0x00705d88`, 64 int32) is the MPEG-1 default intra matrix. Every shipped frame has quant 99, so `f` = 0.5 and one
  table serves them all: 65536, 47248, 59565, ... It is not FFmpeg's `eatqi` table.
- **Macroblock `FUN_006747d0`.** Register convention: `ESI` = the table, `EBP` = the bitstream, `EDI` = the bit
  position. It reads little-endian dwords, most significant bit first (`SHLD`). Six blocks: four luma, then Cb, then
  Cr. Each starts with a DC size code from `0x00fbb92c` (luma) or `0x00fbb9ac` (chroma), added to its predictor at
  table `+0x100`, `+0x104` or `+0x108`. The block tail `FUN_00674ab9` stores `DC * Q[0]` and decodes the AC codes
  through `0x00fbb69c` and the tables after it. `0x41` ends the block. `0x42` escapes to a 6-bit run and an 8-bit
  level, where level 0 reads 8 more bits and level `0x80` reads 8 more bits less 256. Each level times `Q[s]` goes to
  slot `s` of the scan table `0x00fbb5b0`. Nothing else happens to a coefficient: no MPEG-1 mismatch step, no clamp.
- **IDCT.** A block with no AC is filled with `DC * Q[0] >> 17`. Otherwise eight calls of `FUN_0067673c` run over
  the stored rows (a row whose entries 1 to 7 are zero is copied), then eight of `FUN_00676965`, which shifts right
  by 17. The output is pixel / 2, a 7-bit domain. Constants at `0x00fbb598`: `0x5a82799a` (1/√2 through `IMUL` and
  `SHL 1`), 0.5411961, 1.306563, 0.3826834, and 1.5 × 2^52, the float-to-integer rounding constant. The odd part
  rounds through the x87, so the result depends on its precision (the warning below).
- **Colour.** `FUN_00670350` (one caller, `0x006701b7`) builds the YCbCr-to-display tables at run time; it writes
  around `0x00fb9030` (Y), `0x00fb9830` (Cb) and `0x00fba030` (Cr), and the per-channel clamps at `0x00fba730`,
  `0x00fbabf8` and `0x00fbb010`. They read zero in the image.
  The constants it reads are -0.5, 0.3441, 0.5, -1.772, 0.7141 and -1.402 (`0x00705e88`..`0x00705eb0`), BT.601's to
  four places. How it combines them is **not decoded**.

The other codecs' addresses are not TQI's: `DAT_00fb7820` is type 3's table, `FUN_00677140` is the MAD codec's 8×8
fill from `0x00fbc14c` (reached only from `FUN_006771a0`, types 5 to 7), and `0x00fbc018`..`0x00fbc028` is a second
copy of the IDCT constants above, read only by `FUN_006780c1` and `FUN_00678224`.

**Audio.** `FUN_0066ed60` reads the `SCHl` header. Its defaults are 16 bits, 1 channel, 22,050 Hz and codec 0. Tag
`0x81` sets the bits, `0x82` the channels, `0x83` the codec and `0x84` the rate; tag `0x8A` ends it. No shipped
movie sets the rate. `FUN_0066e9a0` decodes one `SCDl` by codec. It is called only while audio is on (`+0x84`), and only for the audio chunk whose place in
a run of consecutive audio chunks equals `+0x7c`; any other chunk resets the count.

- **0**, PCM16. Stereo is copied; mono is written to both channels.
- **7**, EA ADPCM. Stereo goes to `FUN_00672210`, `(size - 12) / 30` blocks of 28 samples; every shipped movie
  takes this path. Mono goes to `FUN_00672090`, `(size - 12) / 15` blocks of 28 samples, written to both channels.
- **10** goes to `FUN_00672cb3`, not read.

The ADPCM step, per channel: `s = ((nibble << 28) >> (shift + 8)) + cur * c1 + prev * c2 + 0x80 >> 8`, clamped to 16
bits. `c1` is from `0x00705ec8` (0, 240, 460, 392) and `c2` from `0x00705ed8` (0, 0, -208, -220). The exe decodes
whole blocks, so it yields a few samples more than the header counts (`bf.tgq`: 194,824 per channel against 194,815). The chunk and the decoded buffers are
counted with `InterlockedIncrement`, which suggests a separate decoding thread.

**Not established.** Which x87 precision the original decodes under. The unicorn runs used `0x027F` (53-bit, the
Windows default); 64-bit gives the same pixels; 24-bit (Direct3D's default) changes 37.5% of the samples of
`bf.tgq` frame 120, by up to 46 of 127 (`fpcw.py`). A movie port should not claim bit-exact until the decoding thread's control word is read in the
original under Proton. Also unread: the colour tables' arithmetic and the skip key `0x006595a8`.

## Tick rates

Both loops step at a fixed 31 ms — about 32 a second — differing only in the catch-up cap (500 ms in the lobby, 2000 ms in a park).

**In the lobby, that step is the particle rate and nothing else.** 0x00520130 is `Particles_Tick`, and it is the entire body of the lobby's tick loop: the single call between the `ADD ECX,0x1f` and the loop test, 0x0054e77c..0x0054e79d. There is nothing else on that tick to find there, so the lobby tells you nothing about the park's other per-tick data rates — those are counted somewhere other than here. A park's loop calls `Particles_Tick` first and then about fourteen more: the RSSE thing engine, a clock-driven scheduler, the crowd and the music level. See `park-engine.md` under "The game clock" for why the step is 31 and not 31.25, and why pausing is nothing but freezing the clock.

In a park:

| Frequency | What runs | Status |
|---|---|---|
| Every tick | 0x00520130 (`Particles_Tick`), 0x00546c80 (the track-ride tick), 0x005516b0 (RSSE). | Verified against the listing. |
| Every 2nd | 0x0055abf0 (the flying cars) and 0x00475360. | Verified: 0054f5c0 reloads `[0x00877d34]`, `TEST AL,0x1`, `JNZ` skips both on odd ticks. This puts the sprite step at 62 ms, exactly its own default interval. |
| Every 8th | 0x00516380 (the thing-list sweep) and 0x0055a470. | Verified: `0054f668` is `TEST byte ptr [0x00877d34],0x7` / `JNZ 0x0054f82d`, and that jump clears both calls — 0x00516380 at `0054f7bb` and 0x0055a470 at `0054f828`. The gate sits **before** the mode dispatch below, so a normal park is gated by both. |
| Every 32nd | Crowd and sound levels. | Verified: `0054f82d`, the target of the every-8th jump, opens `TEST byte ptr [0x00877d34],0x1f` / `JNZ`. Same counter, five bits instead of three. |

Two identifications behind that table:

- **0x00546c80 is the track-ride tick**, not something generic: magic `DAT_00877b58` = 0x4a454647 = "GFEJ", read across 0x00542000–0x0054b000, whose 0x00543560 is the save's **TRAK** module loader.
- **0x0055abf0 is the flying cars**, not the peep simulation.

**0x00516380 is mode-gated AND frequency-gated — both, not either.** It is not online-only, and it does not run every tick. What the listing shows:

- **Mode gating.** `DAT_00fb3b7c` is read at 0054f6c3–0054f754: mode 0 (normal park) and mode 2 (Instant Action) both jump to the call at `0054f7bb`, and mode 1, the ONLINE one, takes 0x005166b0 at `0054f760` instead, which calls it too (`0x005166f2`, when the mode is 1). So all three modes sweep; any other mode does not (`0054f754`).
- **Frequency gating.** `0054f668` is `TEST byte ptr [0x00877d34],0x7` / `JNZ 0x0054f82d`, and it sits **above** that mode dispatch, so taking the jump clears the whole of it along with `CALL 0x00516380` at `0054f7bb`. It falls through only when the counter is a multiple of eight.
- **`DAT_00877d34` is a tick counter**: the Every-2nd row above has `0054f5c0` reloading it for `TEST AL,0x1`. Its own increment is at `0054f4cd` — `MOV ECX,[0x00877d34]` / `INC ECX` / `MOV [0x00877d34],ECX` — and it is reset to zero on state entry at `0054edb0` and `0054f443`.

So in a normal park it is the thing-list sweep that reaches the peeps — it calls 0x0050b360 per thing — **once every eight ticks**, not every tick.

The init guards at 0054f691 / 0054f6d8 / 0054f719 are **not** a divider: they test `[0x00fb34a8] & 1` and fire once. They are simply a different test on a different global, a few instructions below the real one.

**So the sweep runs every 248 ms**: the tick is a derived 31 ms (`park-engine.md`, "The beat is 31 ms and it is derived, not rounded"), and one sweep in eight ticks is 8 × 31 ms.

On OpenTPW's side: there is no `Time.TicksPerSecond`, and no single rate to give it. The sky's 25 is `Sky.TicksPerSecond`, read from FUN_00585f10's 25.0 at 0x00701f7c. The lobby's 10 is `LobbyScript.TicksPerSecond`, read from FUN_005d5c50's 0.01 at 0x007029cc. The game tick is `GameClock.TickSeconds`, 31 ms; the game menu keeps a private 30 of its own.

## Which build this is

`/testme.exe` is version **2.0**, the game's last release (`docs/DECISIONS.md`, "The reference executable is 2.0"). It
says so itself in the two places the 2.0 patch's readme promises:

- **The lobby.** `FUN_0048b020`'s one caller is `FrontEnd_Init`, at `0x005d5a30`, on every entry to the lobby. It
  formats `swprintf( buf, L"v %d.%d", 2, 0 )` and puts the text in a label whose right edge is 0x7c0 and bottom 0x5c0 of
  the 0x800 x 0x600 virtual screen: "v 2.0", bottom right.
- **ALT-V in a park.** The key-table row at `0x00748140` (20 bytes: id 14, key 0x56 'V', modifier word 0x30, which the
  readme calls ALT) runs 0x0040c820, which calls 0x00481b70, a jump into undisassembled code at 0x0048f090 that formats
  `swprintf( buf, L"Ver %d.%d", 2, 0 )`.

`Boot_Init` logs the build as "Compiled Mar 24 2000 at 15:14:05" (above). The PE link time, read from the `TP.ICD` the
exe was dumped from (the dump's own header was overwritten), is 2000-03-24 15:14:32 UTC.
OpenTPW draws neither version string yet.

The import table also names eight EA online libraries, all shipped in the game folder: `weavoter`, `weachatr`,
`weaauthr`, `weanewsr`, `weacityr`, `weauploadr`, `wearasr` and `weamailr.dll` (Q194).

## Under Wine and Proton

Observed with GE-Proton 10-34 (Wine 10); `docs/TOOLING.md` has the recipe.

- **The retail `TP.exe` cannot start.** It is SafeDisc 1.41.000 (the `BoG_ *90.0&!!  Yy>` block). `drvmgt.dll`
  registers the `Secdrv` service with `ImagePath` `system32\drivers\SECDRV.SYS` but never copies the file, so
  `ZwLoadDriver` fails with `c0000142`, the loader faults, and it exits without a message. With the file copied into
  place, `DriverEntry` fails with `c0000001` instead. Both failures come before any disc check.
- **Without the installer's registry values the game quits silently**, before the disc check. With
  `HKLM\Software\Bullfrog Productions Ltd\Theme Park World` (32-bit view) `Language` = `0x409`, `Version` = `"1.1"`,
  `BuildTypeCode` = 0 (what the installer writes), it continues. Which of the three it reads was not isolated; a
  `+reg` trace would settle it.
- **The disc check** needs a drive of type `DRIVE_CDROM`: `HKLM\Software\Wine\Drives` `d:` = `cdrom` (64-bit view),
  plus a label. Wine reads a folder drive's label from `.windows-label`; an `.iso` given as the `d::` device yields no
  label. The check accepts the disc's own label, `TPWORLD`.
- **`2.0_files/tp-2_0.exe`** imports `USP11.dll` (a renamed `usp10.dll`) instead of `USP10.dll`, and skips the disc
  check.
- **Start-up writes `enginedebug.txt` and `fallback.txt`** into the game folder: DirectDraw and Direct3D set-up,
  surface formats, the texture cache, `Renderer: 1` (hardware) with no `Config.tcf`, and `RAM detected: -1` on a
  32 GB machine.

## Addresses

Evidence is a Ghidra trace of `/testme.exe` throughout; the column names what in particular pins the row down.

| Address | Original name | What it is | Evidence |
|---|---|---|---|
| 0x0045a960 | `WinMain_Main` | Entry point: lock, setup, message loop, shutdown. | Decompile |
| 0x0045f640 | | Takes the `CreateMutexA` single-instance lock. | Import call |
| 0x00449df0 | `Flags_LoadDefaults` | Sets the flag word `DAT_007a1a8c` to 0xc0e15. | Decompile |
| 0x00424930 | | Reads `save\config.tcf`. | Filename string |
| 0x00419710 | | Loads language and string tables and keyboard shortcuts from `Data\Language\`. | Path string |
| 0x0045f7a0 | | The disc check, second of the four one-time init steps: builds the drive list, returns `FUN_005aa500`'s answer. | Decompile |
| 0x005aa500 | | Retry/Cancel loop until a CD drive's volume label matches the string at `0x00f7a7c8`; 0 on Cancel. | Decompile |
| 0x005f87b0 | | Lists drives by `GetLogicalDriveStringsA` and `GetDriveTypeA`; mask bit 8 keeps `DRIVE_CDROM`. | Decompile |
| 0x005f89f0 | | `GetVolumeInformationA` for one root, the label into a 20-byte buffer. | Import call |
| 0x005a8730 | | Static initialiser: builds the string at `0x00f7a7c8` from "TPWorld" (`0x007477e8`). | Disassembly |
| 0x0048b020 | | The lobby's "v 2.0" label; its one caller is `FrontEnd_Init`. | Disassembly |
| 0x0048f090 | | ALT-V in a park: formats "Ver 2.0". Undisassembled; reached from key-table row `0x00748140`. | Raw bytes |
| 0x0044e080 | `Window_Create` | Creates the "Theme Park World" window and the D3D device. | Window title string |
| 0x0054dcf0 | `Boot_Init` | The once-only boot init. | Decompile |
| 0x005b5bf0 | | Runs before `Game_StateMachine` on every idle pass. | Call order |
| 0x0054e360 | `Game_StateMachine` | The main state machine; returns bits ORed into `DAT_007a1a14`. | Decompile |
| 0x005503f0 | `Game_Shutdown` | Runs state 0xb if a park is up, then tears everything down. | Decompile |
| `DAT_007a1a14` | | Run/quit word: bit 1 running, 2 quit. | Message loop |
| `DAT_007a1a8c` | | The flag word. | See the flag-word table |
| 0x00550ca0 | | The mode object's constructor, run once per caller's static guard (26 guard bytes across its 85 call sites): derives the type from the flag word (`0x2000000` gives 2, `0x1000000` gives 1, otherwise 0) and writes the type and its bit back. Its "Invalid GameType in SetGameType" assert has a constant true condition, so never fires. | Decompile |
| 0x00550d80 | SetGameType | The only setter taking a type: stores it and sets its one flag bit, asserting `< 3`. Five callers: Select (`0x005c85ae`), 0 or 2 by `gms.dat +0x24`; `Game_StateMachine`, which tests the same byte and pushes 0 (`0x005501b4`) or `EBP`, not resolved (`0x00550179`); `0x004c0876` and `0x005e273e` with 1. | Decompile, call sites |
| 0x004688b0 | | Picks texture folders by the detail setting: `stexture`/`ssharete` or `textures`/`sharetex`. | Folder-name strings |
| `DAT_00785874` | | The texture detail setting read by 0x004688b0. | Read site |
| 0x005d55a0 | | Checks the "Authorization Stamp" and logs the result. | String |
| 0x0040f000 | | Parses options, probably the command line: `version quickload nodebug noload SAVEDEBUG flmouse bwcursor`. Failure aborts the game. | Option strings |
| 0x0040cb80 | | Reads settings sections `system camera cheat coaster shortcuts`. | Section strings |
| 0x0051b660 | | Initialises sound; volumes come from the settings. | Decompile |
| `DAT_00803a2c` | `cat_ui` | The UI sound category (see `lobby.md`); boot plays its effect 0xd2 (210), `BUTTON01` at volume nought. | Call argument; the sound map; a capture |
| 0x00489ca0 | `UI_Init` | Hides the cursor, loads the UI meshes, sets up the UI, calls `Dialup::Initialise`. | "Loaded %d UI meshes" |
| 0x005989c0 | | The per-scene advisor reset (`scenes.md`, "The advisor"). Last call of boot init, before the first state is chosen. | Call order; `scenes.md` |
| `DAT_0087906c` | | The current state. | State machine |
| `DAT_00879068` | | Gate: the state machine runs once this is set, at the end of boot init. | State machine |
| 0x0054df20 | `Intro_PlayBullfrogMovie` | Plays `Data\Movies\bf.tgq`. | Filename string |
| 0x006595a8 | | **Unidentified** key/input check, used alongside ESC and Space to skip the movies. | Call sites in states 5–8 |
| 0x0051b300 | | Returns nonzero when a movie has ended. | States 6 and 8 |
| 0x0051b350 | | Stops the playing movie. | Skip path |
| 0x0054e070 | `Intro_PlayParkMovie` | Plays one park movie chosen by day-of-month & 7. | Filename strings |
| 0x0051b010 | | Starts a movie: fills the settings at `DAT_008023f0`, creates the player, sets its volume ("The movie player"). | Decompile |
| 0x0066e410 | | The movie chunk reader: FourCC to codec type, audio chunks to `FUN_0066e9a0`. | Decompile |
| 0x00670c20 | | The movie frame decoder, by codec type; case 4 is TQI. | Decompile |
| 0x00670890 | | Reads a frame header (TQI: width, height, quant, flags). | Decompile |
| 0x006710c0 | | Builds the TQI dequant table at player `+0x408` from the quant byte. | Disassembly; run under unicorn |
| 0x006747d0 | | Decodes one TQI macroblock (six blocks); `FUN_00674ab9` is its block tail. | Disassembly; run under unicorn |
| 0x0067673c | | TQI IDCT, first pass over the stored rows. | Disassembly |
| 0x00676965 | | TQI IDCT, second pass, `SAR 17`. | Disassembly |
| 0x00670350 | | Builds the movie colour tables at run time. **Arithmetic not decoded.** | Decompile (references only) |
| 0x0066ed60 | | Reads the movie's `SCHl` audio header tags. | Decompile |
| 0x0066e9a0 | | Decodes one `SCDl` audio chunk by codec. | Decompile |
| 0x00672210 | | Stereo EA ADPCM. | Decompile; run under unicorn |
| 0x00672090 | | Mono EA ADPCM; no shipped movie reaches it. | Decompile |
| 0x00550950 | | Loads weather and shadow textures: snow, raindrop, lightning, alphkid. | Texture names |
| 0x00429ba0 | | Loads the advisor model from `%s\Global\Advisor`. | Path string |
| 0x005d5770 | | Constructs the lobby object. | State 1 |
| `DAT_00f82884` | | Holds the lobby object; +0x14 == 1 keeps the lobby loop running. | States 1 and 2 |
| 0x00540900 | | Readies the sprite banks for a scene. Its first argument's low byte goes to `DAT_008768fc`, which lets `SpriteBank_Load` (0x00540d90) take a flagged bank's `.FPC`, and `DAT_00763f80` is set to 1 when that byte is 0 (the `.TPC`s are in). Called (0,1) at the end of the front-end load (0x0054e688) and (0,0) by state 9's park load (0x0054ed2c, `EDI` zeroed at 0x0054e913), so every scene loads its banks from `.TPC`; first person swaps them (`park-engine.md`, "Entering and leaving first person"). Its second argument is not traced. | States 1 and 9 |
| 0x006591e3 | | Opens the bracket around the front-end load. | State 1 |
| 0x00659201 | | Closes that bracket. | State 1 |
| 0x00520130 | `Particles_Tick` | The 31 ms tick body; the lobby's entire tick, and the first of a park's. | 0x0054e77c..0x0054e79d |
| 0x005d5cf0 | | Gives the leave-the-lobby choice; 2 means play a park. | State 3 |
| `DAT_00f7d88c` | | Online object; +0x3f0 keys the online-park branch and the "another park pending" test. | States 9 and 0xb |
| 0x005b50b0 | | Owns that object (ONLINE NEWS). | String |
| 0x00551600 | | RSSE scripts init. | State 9 |
| 0x00457a90 | | Loads advisor paths. | State 9 |
| 0x00407d80 | | Part of the park load. | State 9 |
| 0x00407e00 | | The park's level load: `FUN_005156a0` (which zeroes `mGameTick`), a QuickSave only while `DAT_00788160` is set, then the level (0x00407f20). | State 9, `0x0054ed3f` |
| 0x00457c30 | | Called after the level load (`0x0054ed4e`); the park's save loads after it, later in the same pass (`0x0054f12b`). | State 9 |
| 0x00550b30 | | Runs in state 0xf when not online. | State 0xf |
| 0x00546c80 | | The track-ride tick, every tick. | Magic `DAT_00877b58` = 0x4a454647 "GFEJ" |
| `DAT_00877b58` | | That magic word, read across 0x00542000–0x0054b000. | Read sites |
| 0x00543560 | | The save's **TRAK** module loader, inside that range. | Module tag |
| 0x005516b0 | | The RSSE tick, every tick. | State 10 |
| 0x00516380 | | The thing-list sweep that reaches the peeps. **Every 8th tick**, not every tick — it is mode-gated AND frequency-gated, as "Tick rates" above says. | 0054f6c3–0054f754, gate 0054f668 |
| 0x0050b360 | | Called per thing by that sweep. | Loop body |
| `DAT_00fb3b7c` | | Game mode: 0 normal park, 1 online, 2 Instant Action. | 0054f6c3–0054f754 |
| 0x005166b0 | | Taken in mode 1, the online one, instead of the direct call at `0054f7bb`; it calls 0x00516380 itself (`0x005166f2`). | Same branch |
| 0x00fb34a8 | | Three one-shot init guards OR bit 0 into it and fire once. | 0054f691 / 0054f6d8 / 0054f719 |
| 0x0055abf0 | | The flying cars, every 2nd tick — **not** the peep simulation. | 0054f5c0 |
| 0x00475360 | | The sprite step, every 2nd tick, so 62 ms — exactly its own default interval. | 0054f5c0 |
| `DAT_00877d34` | | The tick counter whose low bit gates the every-2nd pair. | `TEST AL,0x1` at 0054f5c0 |
| 0x0055a470 | | Called at `0054f828`, inside the every-8th gate (its `JNZ` at `0054f66f` skips it), so every 8th tick; not online-only. See "Tick rates" above. | 0054f828 |
| `DAT_00879088` | | Leave-the-park reason: 1 → 0xd, 2 or 3 → 0xb, 3 quits. | State 10 |
| 0x005996d0 | `Advisor_StopQuietly` | Called (1) to stop the advisor with a fade; see `ui.md`. | State 0xe |
| 0x005ac5f0 | | Runs with it in state 0xe. | State 0xe |
