# Boot sequence

How the original game boots: `WinMain` takes a single-instance lock, builds the players, loads the flag word and `config.tcf`, then enters a message loop whose idle pass runs a four-step one-time init (language tables, one unidentified call, the window and D3D device, `Boot_Init`) followed by `Game_StateMachine` on every pass. Boot init picks texture folders, parses options and settings, seeds the random number generator, starts sound, and holds a loading screen while the UI loads. The state machine then walks the two intro movies, the front-end load, the lobby loop, the park load and the in-park loop, and back out again; a single bit of the flag word (0x200) decides whether the front end exists at all. Traced in `/testme.exe` with headless Ghidra. The named functions below (`Boot_Init`, `Game_StateMachine` and the rest) were named in the Ghidra project at the same time; every bare address is still an unidentified `FUN_<address>` there. OpenTPW does not follow this sequence yet — it goes straight to the lobby behind the bar loading screen.

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
6. **Finish:** 0x005989c0 (**unidentified**). The first state is 4 if flag 0x200 is set, else 9.

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
| 9 | **Park load**, behind a new loading screen whose Begin resets the bar. An online-park branch comes first, keyed on `DAT_00f7d88c` +0x3f0 (0x005b50b0's object, ONLINE NEWS). Then: RSSE scripts init (0x00551600), particles, the player (a "debug" slot when there is no front end), weather textures, advisor paths (0x00457a90), 0x00407d80, the QuickSave load (0x00407e00), 0x00457c30. Any failure returns 2, which quits. Game type 1 runs the online chat init, with a 5-minute timeout. `LoadingScreen_End`, then 0xf. |
| 0xf | 0x00550b30 when not online, then 10. |
| 10 | **In-park loop.** Fixed 31 ms ticks with at most 2000 ms of catch-up; see [Tick rates](#tick-rates) for what runs at which frequency. Ticks run only while the app is active or windowed. Also each pass: listener, `Advisor_Update`, render, present, `Scr%05ld.tga` screenshots and the "E W R P S" timings line. `DAT_00879088` == 1 goes to 0xd; 2 or 3 goes to 0xb. |
| 0xd | Goes to 0xe, which calls 0x005996d0(1) to stop the advisor and 0x005ac5f0, then goes back to 10. |
| 0xb | **Leave the park:** teardown. Then 0xc (quit) if `DAT_00879088` == 3 or there is no front end; otherwise 9 if another park is pending (+0x3f0), else 1, back to the lobby. |
| 0xc | Final teardown of the players and online objects; sets the quit bit. |

## Tick rates

Both loops step at a fixed 31 ms — about 32 a second — differing only in the catch-up cap (500 ms in the lobby, 2000 ms in a park).

**In the lobby, that step is the particle rate and nothing else.** 0x00520130 is `Particles_Tick`, and it is the entire body of the lobby's tick loop: the single call between the `ADD ECX,0x1f` and the loop test, 0x0054e77c..0x0054e79d. There is nothing else on that tick to find there, so the lobby tells you nothing about the park's other per-tick data rates — those are counted somewhere other than here. A park's loop calls `Particles_Tick` first and then about fourteen more: the RSSE thing engine, a clock-driven scheduler, the crowd and the music level. See `park-engine.md` under "The game clock" for why the step is 31 and not 31.25, and why pausing is nothing but freezing the clock.

In a park:

| Frequency | What runs | Status |
|---|---|---|
| Every tick | 0x00520130 (`Particles_Tick`), 0x00546c80 (the track-ride tick), 0x005516b0 (RSSE), and 0x00516380 in a normal park. | Verified against the listing. |
| Every 2nd | 0x0055abf0 (the flying cars) and 0x00475360. | Verified: 0054f5c0 reloads `[0x00877d34]`, `TEST AL,0x1`, `JNZ` skips both on odd ticks. This puts the sprite step at 62 ms, exactly its own default interval. |
| Every 32nd | Crowd and sound levels. | **Not re-checked.** This is the one rate on this page that has never been confirmed against the listing. |

Two identifications behind that table:

- **0x00546c80 is the track-ride tick**, not something generic: magic `DAT_00877b58` = 0x4a454647 = "GFEJ", read across 0x00542000–0x0054b000, whose 0x00543560 is the save's **TRAK** module loader.
- **0x0055abf0 is the flying cars**, not the peep simulation.

**0x00516380 is neither frequency-gated nor online-only** — an earlier claim of "every 8th: online 0x00516380 and 0x0055a470" was wrong on both counts and is **refuted**. It is gated on the game mode `DAT_00fb3b7c`, read from the listing at 0054f6c3–0054f754: mode 0 (normal park) and mode 2 (Instant Action) both jump to it, and mode 1, the ONLINE one, is the single branch that does **not** call it, taking 0x005166b0 instead. So it runs every tick in a normal park, and it is the thing-list sweep that reaches the peeps — it calls 0x0050b360 per thing. The "every 8th" was most likely one of the three one-shot init guards at 0054f691 / 0054f6d8 / 0054f719, which OR bit 0 into `[0x00fb34a8]` and fire once, misread as a divider.

On OpenTPW's side: there is no `Time.TicksPerSecond`. The 25 belongs to `Sky.TicksPerSecond`, a private const of the sky's own drift, and the lobby's own rate is `LobbyScript.TicksPerSecond` = 10; **both are still only inferred**. The one rate that is measured rather than inferred is the 31 ms game tick itself, `GameClock.TickSeconds`.

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
| `DAT_00803a2c` | | Sound category used for boot effect 0xd2 (210). | Call argument |
| 0x00489ca0 | `UI_Init` | Hides the cursor, loads the UI meshes, sets up the UI, calls `Dialup::Initialise`. | "Loaded %d UI meshes" |
| 0x005989c0 | | **Unidentified.** Last call of boot init, before the first state is chosen. | Call order only |
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
| 0x00407e00 | | The QuickSave load. | State 9 |
| 0x00457c30 | | Last step of the park load. | State 9 |
| 0x00550b30 | | Runs in state 0xf when not online. | State 0xf |
| 0x00546c80 | | The track-ride tick, every tick. | Magic `DAT_00877b58` = 0x4a454647 "GFEJ" |
| `DAT_00877b58` | | That magic word, read across 0x00542000–0x0054b000. | Read sites |
| 0x00543560 | | The save's **TRAK** module loader, inside that range. | Module tag |
| 0x005516b0 | | The RSSE tick, every tick. | State 10 |
| 0x00516380 | | The thing-list sweep that reaches the peeps; every tick in a normal park. | 0054f6c3–0054f754 |
| 0x0050b360 | | Called per thing by that sweep. | Loop body |
| `DAT_00fb3b7c` | | Game mode: 0 normal park, 1 online, 2 Instant Action. | 0054f6c3–0054f754 |
| 0x005166b0 | | Taken instead of 0x00516380 in mode 1, the online one. | Same branch |
| 0x00fb34a8 | | Three one-shot init guards OR bit 0 into it and fire once. | 0054f691 / 0054f6d8 / 0054f719 |
| 0x0055abf0 | | The flying cars, every 2nd tick — **not** the peep simulation. | 0054f5c0 |
| 0x00475360 | | The sprite step, every 2nd tick, so 62 ms — exactly its own default interval. | 0054f5c0 |
| `DAT_00877d34` | | The tick counter whose low bit gates the every-2nd pair. | `TEST AL,0x1` at 0054f5c0 |
| 0x0055a470 | | Named only in the **refuted** "every 8th, online" claim. | Refuted |
| `DAT_00879088` | | Leave-the-park reason: 1 → 0xd, 2 or 3 → 0xb, 3 quits. | State 10 |
| 0x005996d0 | | Called (1) to stop the advisor. | State 0xe |
| 0x005ac5f0 | | Runs with it in state 0xe. | State 0xe |
