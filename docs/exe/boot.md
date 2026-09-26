# Boot sequence

How the original game boots: `WinMain` takes a single-instance lock, builds the players, loads the flag word and `config.tcf`, then enters a message loop whose idle pass runs a four-step one-time init (language tables, one unidentified call, the window and D3D device, `Boot_Init`) followed by `Game_StateMachine` on every pass. Boot init picks texture folders, parses options and settings, seeds the random number generator, starts sound, and holds a loading screen while the UI loads. The state machine then walks the two intro movies, the front-end load, the lobby loop, the park load and the in-park loop, and back out again; a single bit of the flag word (0x200) decides whether the front end exists at all. Traced in `/testme.exe` with headless Ghidra. The named functions below (`Boot_Init`, `Game_StateMachine` and the rest) were named in the Ghidra project at the same time; every bare address is still an unidentified `FUN_<address>` there. OpenTPW does not follow this sequence yet — it goes straight to the lobby behind the bar loading screen. What it skips on the way is counted once a run in `Game.Run`, before the first lobby: `BOOT_SPLASH` and `BOOT_LEGAL_SCREEN` (`ui.md`, "Loading screen"), and `INTRO_MOVIE_BULLFROG` and `INTRO_MOVIE_PARK` (states 5 and 7). The front end's flag 0x200 is always set here, so the movies are always reached, and all nine `.tgq` ship in `Data\Movies`. The lobby plan cut these (`docs/QUEUE.md`, section G).

## WinMain — `WinMain_Main` 0x0045a960

1. 0x0045f640 takes a `CreateMutexA` lock, so only one copy runs.
2. Setup: creates players (`Players_Construct`); sets the flag word (`Flags_LoadDefaults`); reads `save\config.tcf` (0x00424930); registers `MSWHEEL_ROLLMSG`. The install's `safemode.bat` copies `safemode.tcf`, 32 bytes, over `config.tcf`.
3. Message loop. When idle and running (`DAT_007a1a14` bit 1):
   - **Once**, and every step must succeed or the game quits:
     1. 0x00419710 — language and string tables and the keyboard shortcuts, from `Data\Language\`.
     2. 0x0045f7a0 — **unidentified**; it references no strings.
     3. `Window_Create` 0x0044e080 — the "Theme Park World" window and the D3D device.
     4. `Boot_Init`.
   - **Every idle pass:** 0x005b5bf0, then `Game_StateMachine`. Its return value is ORed into `DAT_007a1a14`, where 2 means quit.
4. On quit, `Game_Shutdown` 0x005503f0 runs. It first runs state 0xb if a park is up, then tears everything down.

## The flag word — `DAT_007a1a8c`

`Flags_LoadDefaults` 0x00449df0 sets it to 0xc0e15. It is replaced only if `dialog.tcf` loads; no such file is in the install, so that is probably a developer settings dialog.

| Bit | Meaning |
|---|---|
| 0x1 | Fullscreen or windowed. |
| 0x200 | **Front end.** Set: intro movies, then the lobby. Clear: straight into state 9 (a park), and leaving the park quits. Nothing found clears it. |
| 0x800000, 0x1000000, 0x2000000 | Game types 0, 1 and 2 (0x00550ca0 and 0x00550d80, "SetGameType"). Type 1 runs the ONLINE CHAT init. |

## Boot init — `Boot_Init` 0x0054dcf0 (once)

1. **Textures:** 0x004688b0 picks the texture folders by a detail setting (`DAT_00785874`): `stexture`/`ssharete` or `textures`/`sharetex`.
2. **Checks and options:**
   - 0x005d55a0 checks an "Authorization Stamp" and logs the result.
   - 0x0040f000 parses options, probably the command line: `version quickload nodebug noload SAVEDEBUG flmouse bwcursor`. If it fails, the game aborts.
   - 0x0040cb80 reads the settings sections `system camera cheat coaster shortcuts`.
3. **Log and random seed:** logs "Compiled Mar 24 2000 at 15:14:05" and seeds the random number generator from `GetTickCount`.
4. **Sound:** 0x0051b660 initialises it, volumes coming from the settings. It then plays effect 0xd2 (210) from category `DAT_00803a2c` — that may be a boot jingle, but **nobody has listened to it**. Then `Sound_ApplyGroupVolumes`.
5. **Loading screen and UI:** `LoadingScreen_Begin` (500 steps) shows the Bullfrog `splash_<lang>.tga` for at least 2.5 s, then `welcome.tga` with the `legal_<lang>.tga` copyright strip and no bar. Behind it, `UI_Init` 0x00489ca0 hides the cursor, loads the UI meshes ("Loaded %d UI meshes"), sets up the UI and calls `Dialup::Initialise`. `LoadingScreen_End` holds the screen until 3 s after it appeared.
6. **Finish:** 0x005989c0 (the advisor reset). The first state is 4 if flag 0x200 is set, else 9.

## Main state machine — `Game_StateMachine` 0x0054e360

The state is `DAT_0087906c`. The machine runs once `DAT_00879068` is set, at the end of boot init.

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
| 9 | **Park load**, behind a new loading screen whose Begin resets the bar. An online-park branch comes first, keyed on `DAT_00f7d88c` +0x3f0 (0x005b50b0's object, ONLINE NEWS). Then: RSSE scripts init (0x00551600), particles, the player (a "debug" slot when there is no front end), weather textures, advisor paths (0x00457a90), 0x00407d80, the level load (0x00407e00; it loads a QuickSave first only while `DAT_00788160`, written by the options parser 0x0040f000, is set), 0x00457c30. Any failure returns 2, which quits. Offline, the park's own save then loads over the new world: the newest `*.TPW*` in the player's theme folder (`FUN_005accf0`, `0x0054f12b`; `park.md`, "Arrivals"). Game type 1 runs the online chat init, with a 5-minute timeout. `LoadingScreen_End`, then 0xf. |
| 0xf | 0x00550b30 when not online, then 10. |
| 10 | **In-park loop.** Fixed 31 ms ticks with at most 2000 ms of catch-up; see [Tick rates](#tick-rates) for what runs at which frequency. Ticks run only while the app is active or windowed. Also each pass: listener, `Advisor_Update`, render, present, `Scr%05ld.tga` screenshots and the "E W R P S" timings line. `DAT_00879088` == 1 goes to 0xd; 2 or 3 goes to 0xb. |
| 0xd | Goes to 0xe, which calls `Advisor_StopQuietly(1)` (0x005996d0) to stop the advisor and 0x005ac5f0, then goes back to 10. |
| 0xb | **Leave the park:** teardown. Then 0xc (quit) if `DAT_00879088` == 3 or there is no front end; otherwise 9 if another park is pending (+0x3f0), else 1, back to the lobby. |
| 0xc | Final teardown of the players and online objects; sets the quit bit. |

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

## Addresses

Evidence is a Ghidra trace of `/testme.exe` throughout; the column names what in particular pins the row down.

| Address | Original name | What it is | Evidence |
|---|---|---|---|
| 0x0045a960 | `WinMain_Main` | Entry point: lock, setup, message loop, shutdown. | Decompile |
| 0x0045f640 | | Takes the `CreateMutexA` single-instance lock. | Import call |
| 0x00449df0 | `Flags_LoadDefaults` | Sets the flag word `DAT_007a1a8c` to 0xc0e15. | Decompile |
| 0x00424930 | | Reads `save\config.tcf`. | Filename string |
| 0x00419710 | | Loads language and string tables and keyboard shortcuts from `Data\Language\`. | Path string |
| 0x0045f7a0 | | **Unidentified.** Second of the four one-time init steps; references no strings. | Call order only |
| 0x0044e080 | `Window_Create` | Creates the "Theme Park World" window and the D3D device. | Window title string |
| 0x0054dcf0 | `Boot_Init` | The once-only boot init. | Decompile |
| 0x005b5bf0 | | Runs before `Game_StateMachine` on every idle pass. | Call order |
| 0x0054e360 | `Game_StateMachine` | The main state machine; returns bits ORed into `DAT_007a1a14`. | Decompile |
| 0x005503f0 | `Game_Shutdown` | Runs state 0xb if a park is up, then tears everything down. | Decompile |
| `DAT_007a1a14` | | Run/quit word: bit 1 running, 2 quit. | Message loop |
| `DAT_007a1a8c` | | The flag word. | See the flag-word table |
| 0x00550ca0 | "SetGameType" | Sets game type 0/1/2 in the flag word. | String |
| 0x00550d80 | "SetGameType" | Second of the pair. | String |
| 0x004688b0 | | Picks texture folders by the detail setting: `stexture`/`ssharete` or `textures`/`sharetex`. | Folder-name strings |
| `DAT_00785874` | | The texture detail setting read by 0x004688b0. | Read site |
| 0x005d55a0 | | Checks the "Authorization Stamp" and logs the result. | String |
| 0x0040f000 | | Parses options, probably the command line: `version quickload nodebug noload SAVEDEBUG flmouse bwcursor`. Failure aborts the game. | Option strings |
| 0x0040cb80 | | Reads settings sections `system camera cheat coaster shortcuts`. | Section strings |
| 0x0051b660 | | Initialises sound; volumes come from the settings. | Decompile |
| `DAT_00803a2c` | `cat_ui` | The UI sound category (see `lobby.md`); boot plays its effect 0xd2 (210). | Call argument |
| 0x00489ca0 | `UI_Init` | Hides the cursor, loads the UI meshes, sets up the UI, calls `Dialup::Initialise`. | "Loaded %d UI meshes" |
| 0x005989c0 | | The per-scene advisor reset (`scenes.md`, "The advisor"). Last call of boot init, before the first state is chosen. | Call order; `scenes.md` |
| `DAT_0087906c` | | The current state. | State machine |
| `DAT_00879068` | | Gate: the state machine runs once this is set, at the end of boot init. | State machine |
| 0x0054df20 | `Intro_PlayBullfrogMovie` | Plays `Data\Movies\bf.tgq`. | Filename string |
| 0x006595a8 | | **Unidentified** key/input check, used alongside ESC and Space to skip the movies. | Call sites in states 5–8 |
| 0x0051b300 | | Returns nonzero when a movie has ended. | States 6 and 8 |
| 0x0051b350 | | Stops the playing movie. | Skip path |
| 0x0054e070 | `Intro_PlayParkMovie` | Plays one park movie chosen by day-of-month & 7. | Filename strings |
| 0x00550950 | | Loads weather and shadow textures: snow, raindrop, lightning, alphkid. | Texture names |
| 0x00429ba0 | | Loads the advisor model from `%s\Global\Advisor`. | Path string |
| 0x005d5770 | | Constructs the lobby object. | State 1 |
| `DAT_00f82884` | | Holds the lobby object; +0x14 == 1 keeps the lobby loop running. | States 1 and 2 |
| 0x00540900 | | Called (0,1) at the end of the front-end load. | State 1 |
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
