# Scenes: what the lobby and a park share

The original executable builds a small number of systems once at boot and keeps them alive for the whole process — the UI root and its click hook, the player list, the sound engine and its six global categories, and the game clock. A scene (the lobby, or a park) never rebuilds those; its UI setup only empties the message queues. What a scene does own is the advisor's models, its own sound categories, its particle and sprite banks, and its view lists, and those are torn down in a fixed order when the scene is left. Several user-facing functions — the message box, the options screen, the game menu — are single functions that branch on which scene is running. This page records what is shared, what is per-scene, the exact teardown order, the route from Escape in a park to the game menu, and the advisor's two data tables.

## Lives for the whole process

| Address | Original name | What it is | Evidence |
|---|---|---|---|
| `0x00489ca0` | `UI_Init` | Builds the UI root and the click hook; reached only through the thunk at `0x004813c0`, called from `Boot_Init` | Single call path traced from `Boot_Init` |
| `0x004813c0` | — | Thunk, the only caller of `UI_Init` | Call-site trace |
| `FUN_006584df` | — | The UI root object built by `UI_Init`, freed only in `Game_Shutdown` | Freed nowhere else on any traced path |
| `DAT_00faa5fc` | — | The global click hook built by `UI_Init`, freed only in `Game_Shutdown` | As above |
| `FUN_00659064` | — | Empties the UI message queues; this is all a scene's UI setup does to the shared root | Scene setup call sites |
| `0x0045aa5a` | — | Point in `WinMain` where `g_Players` is built; the list is freed in state `0xc`. "Exit To Lobby" keeps the current player | State-machine trace |
| `FUN_005c8650` | — | Save the current player and deselect. Called from exactly three sites: `0x0045acfc`, `0x0055024a`, `0x0048bc58` | Complete xref set |
| `0x0051eae0` | `Sound_RegisterGlobalCategories` | Loads the six global sound categories at boot | Boot trace |
| `0x785970` | — | The game clock. Both the lobby loop and the park loop tick it at 31 ms; only a park ever pauses it | Both loops traced; pause call sites are park-only |
| `0x00520130` | `Particles_Tick` | The clock tick itself. It is the whole of the lobby's tick loop, and the first call in a park's | Loop bodies compared |

The group volumes and the speech duck are globals of the sound engine, not per-scene state. The clock's field layout, its step and its catch-up caps are documented with the park engine.

The help bar is also built in `UI_Init`, but through a virtual slot, so its once-only lifetime is **only partly proven**.

## One function, both scenes

| Address | Original name | What it is | Evidence |
|---|---|---|---|
| `0x0047f020` | `MessageBox_Open` | The modal message box, 27 callers across both scenes. Pauses the game only when `g_ParkRunning == 1` and `DAT_00f82884 == 0` | Xrefs; branch condition read from the disassembly |
| `0x004a3a30` | `OptionsScreen_Open` | Game Options, shared. Hides the park world (`DAT_007cb2ac`) or the lobby window depending on the caller, and calls `Advisor_StopQuietly(1)` | Called from the lobby menu at `0x0048bf28` and from park menu case 5 at `0x0048b977` |
| `0x0048c830` | `GameMenu_Open` | Dispatches to the lobby or park build | Branch on the running scene |
| `0x0048c600` | `GameMenu_BuildLobby` | Lobby game menu; its handler is `0x0048bd40` | Menu construction trace |
| `0x0048c150` | `GameMenu_BuildPark` | Park game menu; its handler is `0x0048b6a0` | Menu construction trace |
| `0x0048b6a0` | — | Park menu handler. Actions: Resume 0, Load 1, Save 2, Restart 3, Publish 4, Options 5, Exit To Lobby 6, Go Offline 7, Quit 8 | Switch table |
| `0x0047ed80` | `UI_LoadModalTree` | Loads a modal dialog: adds a full-screen backdrop as the **last** child, which swallows the pointer, and gives focus to the dialog's root | Child order and hit test read from the disassembly |
| `FUN_006698e6` | — | The UI's key pop: hands a key to one control - an accelerator's target, else the focus if it is visible, else the last control any press reached, if visible | Key routing trace; `lobby.md`, "The lobby's keys act on the release" |

A key goes to one control only: an accelerator's target (the one table, `0x0077c4b8`, is a list's End, Home, Up and Down), else the focused gadget if it is visible, else the last-pressed one (`FUN_006698e6`). A control that does not handle it drops it. Keys never go through the hit test.

## The park Escape route

| Address | Original name | What it is | Evidence |
|---|---|---|---|
| `0x007cb2ac` | — | The park world control; it holds focus while a park runs, so it receives the key | Focus trace |
| `FUN_0040cb80` | — | Builds the binding tables: system, game, camera, cheat, coaster, shortcuts. Entries are `0x14` bytes | Table construction read from the disassembly |
| `FUN_0040caa0` | — | Rebinds an entry in those tables | Call-site trace |
| `0x0040c4d0` | — | Shortcuts action 0, "menu" | Binding table entry |
| `0x004816d0` | — | Called by the shortcut, calls `GameMenu_Open(0)`. **Undisassembled bytes, so xrefs do not find it** | Read by hand at the address |
| `0x00488921` | — | `Park_MouseMessageProc`'s key-up case (`0x1000b`): the chain below through `FUN_0040c990`, which runs a row's `+0x0c` handler. Its key-down case (`0x00488886`, `0x1000a`) walks the same chain through `FUN_0040c900`, which only latches the row | Disassembly |
| `0x0048bb36` | — | The park menu handler's (`FUN_0048b6a0`) key-up case: closes the menu and lets the park run again when the key is `0x1b`, **whatever the modifiers**, or when key and modifiers find shortcuts row 0 (`FUN_0040c870`). It answers no key-down | Disassembly |
| `0x00748028`, `0x00748378` | — | The game table's rows and the shortcuts table's rows. Row 0 of each is key `0x1b` with modifier 0: game action 0 (`0x0040c180`) and "menu" (`0x0040c4d0`). No other row of the four tables on the chain is `0x1b` (camera `0x00748158`, cheat `0x007484d0`), nor of the system table (`0x00747f38`). The coaster table's row 0 (`0x00748350`) is: `abortcoaster`, `0x0040c3a0`, run only by the coaster bar's `FUN_004982a0` | `read_memory`, 20-byte rows |

The chain, in order, on the key's release: the focused world control `0x007cb2ac` takes the key, and its handler `Park_MouseMessageProc` (`0x004881a0`) runs the camera, game and shortcuts tables built by `FUN_0040cb80`, then the cheat table behind a guard, stopping at the first handler that answers non-zero. Game action 0 (`0x0040c180`) closes the staff/visitor locator if it is open (`FUN_004816b0`, `DAT_007cc2f0`); otherwise, over any interaction mode but idle, it installs the idle mode and answers 1 - see `park-engine.md`, "The hand's ways out". Only with the mode idle already does shortcuts action 0 "menu" (`0x0040c4d0`) run, which calls `0x004816d0`, which calls `GameMenu_Open(0)`. In first person the key goes to layer 1's `FUN_00488a00` instead, which runs camera-table handlers only, so the menu cannot open from there.

**Every arm acts on the release, and only the chain asks for no modifier.** The menu's handler and the viewfinder's
(`FUN_00488a00`, whose key-up case tests `0x1b` before any action) compare the key alone, so Shift+Escape closes the
menu or leaves first person. Game row 0 and shortcuts row 0 name modifier 0, which a row must match exactly
(`park-engine.md`, "How a key is matched"), and no other row on the chain is Escape, so with a modifier held Escape empties
no hand and opens no menu. `FUN_0040cb80` rebinds rows from a key resource (`DAT_0078ba84`, not read) through
`FUN_0040caa0` (`0x0040ce64`..`0x0040cf54`, read with `ECX`) and inline for shortcuts actions 1-4: shortcuts actions
1-11 and 13-16, game actions 1, 2, 9 and 10, system actions 0 and 1. Neither Escape row is among them.

**A park screen in front has the key instead.** The six management screens, an object window and the map are each
handed to `FUN_00485b70`, which records the screen (`DAT_007c24c8`, `0x00485d02`) and calls `FUN_004862a0`; while
layer 0 is shown, that gives the screen the focus (`0x00486384`..`0x00486390`), and the map takes it itself. Their
handlers pass a key-up to `FUN_00488ba0`, which answers a plain Escape - key `0x1b` and no modifier byte
(`0x00488bc6`) - by sending the screen message 4, which closes it, and hands any other key to the shortcuts table alone
(`0x00488c13`). The map's handler does the same (`0x005f17ef`). So over a screen, Escape closes it and nothing more.
The message box's root drops keys, and the options screen hides the layer.

**The modifiers are read at each key-up.** The window procedure `FUN_0046b600` asks `GetKeyState` (`[0x006fd324]`) for
Shift, Ctrl and Alt at `0x0046bb0b`..`0x0046bb39`, just before `UI_PostKey` (`0x0046bb4f`), so each release carries
the modifiers as they stood when it came up.

**OpenTPW** (`ParkFrontEnd.ParkKeys`, `MenuKey`): Escape is read from the frame's releases, once for each, with the
front window read again for each; the menu and first person take it whatever the modifiers, and the hand and the menu
only with none held. The lobby's is `FrontEnd.LobbyKeys` (`lobby.md`, "The lobby's keys act on the release").
**Not the original's:** the park screens are modal here and keep Escape, and an object window lets it through to the
hand and the menu (Q119); the modifiers are judged as the frame ends, as every binding here is (Q120).

## The advisor

There is one speaker, driven from two states, and each scene feeds it a different way.

| Address | Original name | What it is | Evidence |
|---|---|---|---|
| `0x0054e6df` | — | `Advisor_Update` call site in state 2 (lobby) | State body |
| `0x0054f9f9` | — | `Advisor_Update` call site in state 10 (park) | State body |
| `g_FrontEnd+0x14` | `AdvisorQueue` | The lobby's queue, vtable `0x00702da0`; ticked only in state 2 | Vtable and tick site |
| `FUN_0059a550` | `CAdvisor` | The park's feed, run every 8th tick. Calls `Advisor_SayResponse` directly — say-if-free, **no queue** | Tick modulus and call read from the disassembly |
| `0x004a6580` | `FrontEnd_ShowPlayerSlots` | The only site that pushes the lobby greeting | Complete set of greeting pushes |
| `0x004a61b0` | — | The delete tick; calls `FillPlayerSlots` and does **not** greet | Call path checked |
| `0x004a62b0` | `FrontEnd_FillPlayerSlots` | Refills the slot gadgets without greeting | As above |
| `FUN_005989c0` | — | Per-scene advisor reset | Scene setup |
| `FUN_00598ad0` | — | Per-scene advisor stop; models are freed and `Sound_SetSpeechDuck(0)` runs at the end of states 1 and 9 | Scene teardown |

Lobby lines are pushed at front-end call sites, through the queue. Park lines bypass the queue entirely.

### Response table `0x00768fb8`

Laid out in `advisor-park.md`, "`AdvisorResponseTable` at `0x00768fb8`".

### Gesture table `0x0076dc18`

Rows are `0x50` bytes.

| Row | Cue | Used by |
|---|---|---|
| 12 | 17000 | The lobby tour, response 393 |
| 13 | 1000 | Responses 374–378 and 446–469 |

A cue counts from the end of the preceding clips, not from the start of the line.

## Leaving the lobby (state 3), in order

1. `FUN_005d5cf0`, which does three things in this order:
   1. the `+0x10` stop on every child. `g_FrontEnd`'s stop is `0x005e4140`, which stops its own children in turn; the queue's is `AdvisorQueue_Clear` `0x005d6060`, which calls `Advisor_StopSpeaking` when the advisor is busy — so he **cries** if his voice is sounding at that moment (vtable slots `0x00702d88` and `0x00702db0`);
   2. its deletes, including `FUN_0051ea50` freeing `cat_globallobbysfx`;
   3. message 4 to the lobby tree.
2. `FUN_005d5840`
3. `FUN_0048a3d0` — the lobby UI
4. `FUN_00598ad0` — advisor stop
5. `FUN_00429e20(0)` — the advisor model
6. `FUN_00576820` — view lists
7. `FUN_0051bcb0` — fade every voice, argument 90; **the unit of that argument is unknown**
8. `Particles_Shutdown`
9. `FUN_00541ed0` — sprite banks

Leaving a park (state `0xb`) has the same shape, and additionally frees the level sound categories with `FUN_0051ee40`.

## Sound scopes

**Global.** The six categories loaded at boot by `0x0051eae0`.

**Lobby.** `cat_globallobbysfx`, registered in `FrontEnd_Init`, plus a `locallobby` pair per island.

**Park.** Ambient, rides, speech and music, loaded in state 9 by `FUN_0051ec50` (called at `0x0054ec92`) into `DAT_00803a38`, `DAT_00803a3c`, `DAT_00803a40` and `DAT_00803a44`. On disk these are three directories, which is why the function has three blocks: `levels/<theme>/Sound/` holds `cat_ambient` and `cat_rides`, `Speech/` holds `cat_speech`, `Music/` holds `cat_music`.

### What a park actually plays

| Address | Original name | What it is | Evidence |
|---|---|---|---|
| `0x0051bd70` | `Sound_ApplyGroupVolumes` | Re-applies the group volumes on park entry; called at `0x0054ec9a` | State 9 body |
| `FUN_0051e730` | — | Called at `0x0054ec9f`; plays `cat_music` effect 2, the only effect that category declares | State 9 body; category contents |
| `FUN_0051bc40` | — | `FUN_0051bc40(voice, 4, 0)` immediately sets that voice's level to 0; op 4 is "set level" | Called straight after the play |
| `FUN_0051e790` | — | Called at `0x0054f870` every pass of the park loop; drives the music level | Park loop body |
| `FUN_004c81e0` → `FUN_004c7fa0` → `FUN_004fa990` | — | The counting chain behind that level: things that pass one of five type tests | Call chain traced |
| `FUN_00550e00` | — | Reads placed emitters from the level's `scape.omp`: an `OBJ_` chunk of record count, record size, then a dispatch on field[0]; type 1 is a placed sound | Chunk layout read from the loader |

The level is `clamp(things / 2, 0, 100)`, further clamped to 89. So the original's park music swells with the crowd, and an empty park is silent. Everything else audible in a park — the ambience especially — is a placed emitter read by `FUN_00550e00`.

### Positioning

Lobby sounds are flat, emitted at `(0, 0, 0)` with a fixed listener at `(0, 0, -50)`. **Confirmed exactly, 2026-09-21:** the lobby's listener call at `0x0054e70e` passes nine literals — position `(0, 0, -50)` (`0xc2480000` = −50.0f) with orientation vectors `(0, 0, 1)` and `(0, 1, 0)`. That `(0,1,0)` top vector is what independently establishes the original is **Y-up**, and therefore that the coordinate a pause overwrites with 10000.0 is **height**.

**Park sounds are positional — but "with the camera as the listener" is incomplete.** A park has **two** listener call sites, chosen by the camera-mode mask `[0x00790ab0] & 0x16`:

| Site | When | Listener position |
|---|---|---|
| `0x0054f958` | mask set | the camera globals `0x007909c0` / `c4` / `c8` |
| `0x0054f9e4` | mask clear | the **midpoint** between the camera and a second point (`0x00790ab8` / `bc` / `c0`), lerped by the `0.5` at `0x00700f88` |

Both share the orientation globals `0x007909b0`/`b4`/`b8` and `0x007909a0`/`a4`/`a8`. The camera block is written by `FUN_0042a990` and `FUN_0042b1c0`, which sets the orientation globals from a matrix's rows (`0x0042cb63`, `0x0042cb70`) and writes `0x00790ab8` at `0x0042c6ac`, right beside its own `TEST CL,0x16` on the same mask.

All three sites call **`FUN_0051c1d0`**, which is the per-frame listener update — see `audio.md`, "The listener, and what a pause does to it".

### Park parameters

Music, crowd and rain are driven by voice parameters. That the parameter id is the `SFX.map` effect `field4 >> 16` is **inferred, not verified**, and what a parameter actually does is **undecoded**.

## Particles, sky and weather

The particle code and `Tp2.plb` are the same in both scenes, but init and shutdown happen per scene; a park uses the world-space branch of `Particles_Render`.

The sky loader is shared. The lobby passes `levels/fantasy` and height 180 through `FUN_00585690`, which is its only caller; the object default is 300.

The weather renderer is shared, but the lobby and a park have separate drivers, and each driver plays its own thunder.
