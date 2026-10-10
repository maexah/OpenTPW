# Tooling recipes

The planned recipes file (`docs/README.md`). The recipes `docs/MEMORY-DIET.md` still owes it stay owed. Machine
paths are in `CLAUDE.local.md`.

## The tools every item reuses

`CLAUDE.md` rule 21: read this before writing a harness script, use what is here, and add a row for anything built
that a later item could use. The files are outside the repo, in the harness folder (`CLAUDE.local.md`): `lib/` is
shared by every item, `original/` drives the original, and a `q<N>/` folder is one item's own evidence, not a
place to look for tools. Each file's docstring is its manual.

| Tool | Use it for |
|---|---|
| `lib/parkrun.py` | The scaffold of every save-and-load confirm run: a private game folder, `enter()`, `save()`, `load()`, `reply()`, `gaps()`, `shot()`, `predict()` and `said()`, `done()`; `OFFSCREEN = True` for a run beside the desktop's; `original_view( x, y )` for a saved camera the original's frame will show the point in |
| `lib/parkfile.py` | The one reader of a park file: `Park` (things, cells, sprites), `track()` (the track-rides module, cars and riders), `models()` (every model record: item, flags, node words, lookup records, channels), `head()` (a sprite). Add a module's reader here, not in an item's folder |
| `lib/mutate.py <mutations.py>` | Putting an item's bugs back, in parallel, in copies of the tree (`WORKFLOW.md`, "Verifying") |
| `lib/stryker.sh <project> <File.cs>...` | Mechanical bugs in a file, Stryker.NET |
| `original/loadfile.sh load <file> <outdir> <tag>` / `stop` | A park file loaded in the original from its Load Park list, off-screen, with frames; `POLL=` starts a memory reader just before the click. It starts the original only when it is not running, so a second file is 35 s where the first is 123 s |
| `original/research.py <in> <out> <item>` | A copy of a jungle park file with one item marked researched, so Instant Action's buy list offers it |
| `original/original.sh`, `tpwmem.py`, `gmove.py`, `record.sh`, `watch.sh` | Starting, reading, clicking, filming and watchpointing the original ("The original under Proton", below) |
| `q119/lib.py` | What `parkrun.py` is built on: the launch, the console pipe, XTEST clicks and keys, the frame grab. Use it through `parkrun.py` |
| `gen_addresses.py` | Regenerating `docs/exe/addresses.md` |
| `wadcat`, `strdump`, `nodenames/` | A wad's entries, a string table's lines, a UI wad's node names (`CLAUDE.local.md`) |

**A poller of the original's memory is the one thing still written an item at a time** (`q257r/orig/cars.py`,
`q257d/orig/look.py`, `q253/orig/look.py`): each reads its own structures. Start from the newest that reads the same
structure, and hand it to `loadfile.sh` through `POLL`.

## Recipes

Each was found by trial in the item named, and cost a run to find.

**In OpenTPW, through the console** (`OPENTPW_DEBUG_CONSOLE=1`; the `case` labels in `DebugConsole.cs` are the
whole list):

- **`camera x y zoom yaw` takes world units**, a cell times ten (Q257n).
- **Open a bought ride:** `buy <item> <x> <y> 0` answers the thing's id and its queue node's cell; then
  `tool queue <thing>`, `worldclick <the node's cell>`, `worldclick <a path cell beside it>`, `tool off`. For a Hot
  Pot on (41,23): `buy 1140 41 23 0`, node (43,22), path (43,21); four boats are out six seconds on (Q257r).
- **Fill a ride:** `admit <x> <y>` on a path cell answers a new guest's id, `send <guest> <thing>` sends them; poll
  the ride's census (`bumpers`, `rides`) until it counts them (Q179d, Q257r).
- **A Balloon Shop a guest can reach:** `buy 1209 43 22 0` (the review of 2026-10-06, fix 2).
- **Take the census last.** `step 2` after a `camera` moves every car and guest two ticks: a census held against a
  file must be taken after the last step before `savepark` (Q257r).
- **A load runs on before `pause` lands**, a dozen ticks: hold a loaded park against the load's own log lines, not
  against a census (Q257r).
- **A run that needs changed options** uses a private game folder, links to the real one and a `save/` of its own
  (`parkrun.py` makes one; the review of 2026-10-06, fix 3).
- **In a test, a boat is launched empty and then filled:** `Launch`, `Board`, `Fill`, as the ride's script does
  (`BUMP 4`, `1`, `12`); `Board` before `Launch` leaves the car not driven (Q257r).

**In the original, off-screen at 1024 x 768** (points for `gmove.py X Y`; `gmove.py align` first, with `GW=1022
GH=766` there, or the pointer is off):

- **It enters a park on the newest file in the player's folder**, so a file copied in before the start is loaded at
  the first frame, and again by the list's click (Q257n). Its track tick is not reset by a load (Q257p).
- **The player screen:** the slot (660,58), clicked twice: the first click is ignored.
- **The game menu:** Escape with no tool in hand opens it **and pauses the game**; never sleep under it. Load
  (510,50) then the first row (400,123), the newest file; Save (510,140), type the name, Return; Options
  (510,400), the quality track (512,276), OK (898,688).
- **Buying:** Buy (117,588), a ride's row such as (560,305), the features tab (843,123). A blue footprint is a
  legal place and a red one is not. After the placing click the queue tool is in the hand: a click on the path
  joins the queue and puts the tool away, and a later click on the queue's cell picks it up again (Q257o).
- **Hiring:** Buy (117,588), the side tab (957,316), mechanics (665,123).
- **Instant Action offers only researched items:** patch a copy with `research.py`, never the reference
  `easymode.TPWI` (Q257n).
- **Its camera stands further back than OpenTPW's for the same numbers.** A file saved under
  `parkrun.original_view( x, y )` shows the point mid-frame there; measured at zoom 110 and yaw 180 alone (Q257r).

## The original under Proton

The original game (`testme.exe`, the 2.0 build Ghidra holds) runs on this machine through GE-Proton, as a
reference instrument (`QUEUE.md` Q168). It is Linux only: Proton is Wine for Linux. It is not an emulator and it
does nothing on Windows or macOS.

### What it gives you

- **The original's frames** beside OpenTPW's: the same park and state, captured from the real screen.
- **Live memory at Ghidra's addresses.** `testme.exe` has image base `0x400000` and no relocations, so a Ghidra
  address is the address in the running process. `tpwmem.py read` and `tpwmem.py watch`; reading is safe.
- **Timings** (a tick rate, a walk, an animation), **only after `tpwmem.py clock` says TRUE.** See the first
  warning.
- **Traces of what the game asks Windows for**: `ORIG_WINEDEBUG=+file original.sh start` logs every file it opens,
  `+reg` every registry read, `+dsound` its sound calls. `+ddraw` and `+d3d` are heavy. Logs go to the harness's
  `logs/`.
- **The game's own logs**: `enginedebug.txt` and `fallback.txt` in the install folder, rewritten at every start
  (display mode, renderer, texture cache).
- **Files written by the original**: `save\Config.tcf`, players and parks in the install's `save/`. They are ground
  truth for the FileFormats docs.

### Running it

The harness, named by file here (`CLAUDE.local.md` says where it lives):

| Command | What it does |
|---|---|
| `original.sh start` | Starts the reference install on the real screen, silent (null ALSA), capped at 30 fps, with the Proton log and a frame-rate log |
| `original.sh start --offscreen` | The same on a private Xvfb `:77`, with a private KWin (own D-Bus and config folders) so the picture is scaled |
| `original.sh stop [--offscreen]` | `wineserver -k` for the reference prefix only; with the flag, also its private KWin and Xvfb |
| `original.sh pid` | The game's Linux pid |
| `loadfile.sh load FILE OUTDIR TAG` / `stop` | Loads a park file from the Load Park list, starting the original only if it is not running; frames, and a reader through `POLL=` |
| `tpwmem.py clock [s]` | Tick rate and park-clock speed against real time; exits 2 unless both are within 5% |
| `tpwmem.py read 0x00877d34 0x00785988:d` | Reads addresses (`I` u32 default, `i h H B f d`, `sN` raw bytes) |
| `tpwmem.py watch ADDR [s]` | Prints each change |
| `gmove.py align` / `gmove.py X Y [click]` | Lines the game's cursor up with the pointer, and glides in small steps |
| `record.sh start NAME` / `stop` | Films the off-screen display, lossless, 30 frames a second, with the wall clock at its start |
| `record.sh frames NAME FROM TO` / `sheet NAME FROM TO` | Every frame of the film between two of its seconds, each with its wall-clock time, or the same as one contact sheet |
| `watch.sh 'EXPR' SIZE [HITS] [SECS]` | Which code writes a piece of memory: a hardware watchpoint through `gdb`, attached and detached again |

**Film what lasts under a second.** A timed still misses it: a balloon's burst lasts a second there, and Q255's
three stills caught none of it. Start `record.sh` before the click and pull the frames after; a frame's wall-clock
time (good to 0.2 s) lines it up with a memory poll that prints its own. Tried 2026-10-09: 158 frames in 5.3 s.

**Ask who wrote it, do not infer it.** `watch.sh` prints the program counter after each write to an address and
the value written; the instruction before that counter is the writer, at the listing's own address (the game is
a 32-bit process, loaded at its image base). Tried 2026-10-09 on `mGameTick`
(`'*(unsigned*)0x7cf83c + 0x1da70c'`): five writes, 806 to 810, each from `0x0051639e`, after the
`MOV [EDI+0x1da70c],ECX` at `0x00516398` in `FUN_00516380`, and the game ran on after the detach. The game is
held while `gdb` attaches (about a second) and for a few milliseconds a hit, so a count of sweeps across a watch
is the park clock's, not the wall's. Four watchpoints at most, the processor's.

**Getting to a park:** "Welcome to Sim Theme Park" (click once if it waits) → intro films on a fresh `save/` (click
through) → player screen. **The first click on the player screen is ignored**, so click again. Then type a name,
click the tick, click the island. The Lost Kingdom park loads in about 30 s.

**Mouse:** the game reads relative motion (DirectInput). Run `gmove.py align` first, which sweeps corner to corner so
the game's cursor clamps onto the pointer. Glide rather than jump, and hold the button about 0.2 s.
**Coordinates:** the game's 4:3 picture fills the screen's height and is centred. On a screen S_w × S_h, with the game
at height H, a game point (x, y) is at screen ((S_w − S_h·4/3)/2 + x·S_h/H, y·S_h/H). Off-screen at 1024 × 768 it
fills the screen.

### Warnings, most important first

1. **The park clock is wrong once the computer has been on for days.** The park clock is a double of
   "milliseconds since boot", added to each frame at 24-bit precision (`docs/exe/park-engine.md`, "The park clock
   loses precision with uptime"). Measured at 6.5 days of uptime: uncapped, the park froze (0 ticks in 30 s); at 30 fps
   it ran 1.38 to 1.54 times real time; at about 4.5 fps it ran at real time. **Run `tpwmem.py clock` before any
   timing, and discard timings it calls NOT TRUE** (`VERIFYING.md` rule 127). The fix is a restart, and the ask is
   Alexah's. The 30 fps cap stays on.
2. **Never point it at `~/Games/TPWorld`.** The original writes `save/`, `Config.tcf`, `enginedebug.txt` and
   `fallback.txt` into its own folder. The reference install is its own copy.
3. **On the real screen it takes the whole display and the mouse.** Start it only when the other session is idle
   and Alexah has agreed to the run (`CLAUDE.md` rule 12). Your monitor's mode is not changed: Proton scales the
   game's mode.
4. **Off-screen is for memory and logic, not pictures.** llvmpipe drew Lost Kingdom's gate without its sign
   (`VERIFYING.md` rule 128). Without a window manager, the picture is a cropped corner; `original.sh` starts one.
5. **The first frame grab after a scene change can repeat the old scene.** Take two, a few seconds apart.
6. **One copy per prefix.** The game's `CreateMutexA` lock (`docs/exe/boot.md`) makes a second start quit silently.
   `original.sh stop` first.
7. **Sound:** silent by default. With no audio device at all, the game crashes right after its sound start-up
   (`docs/exe/audio.md`). An audible run needs Alexah's word.
8. **Registry writes** (`VERIFYING.md` rule 130): with the game closed; with `/reg:32` or `/reg:64` stated; checked
   in `system.reg` afterwards. The game needs the installer's `Bullfrog Productions Ltd\Theme Park World` values,
   which `tpw-setup.sh` writes, or it quits silently at boot.
9. **Kill by pid, never `pkill -f PATTERN`** when the pattern is also in your own command line
   (`VERIFYING.md` rule 129).
10. **Disassembly is Ghidra's job** (`CLAUDE.md` rule 7). If the Ghidra server is down, say so rather than reaching
    for `objdump`.
11. **A big park off screen draws at about a hundredth of a frame a second** under llvmpipe. With
    `GALLIUM_DRIVER=zink MESA_VK_WSI_DEBUG=sw` in front of `original.sh start --offscreen` it draws on the GPU through
    Vulkan at 30 frames a second, but its frame grabs read black: memory only.
12. **A player's saved park** loads from a copy of that player's folder in the reference install's own `save/users/`,
    under a slot digit no other folder there has (a folder's leading digit is its slot, and two alike show one).
    Entering the island loads it. An Instant Action player loading a Full Simulation park crashes the load (a page
    fault at `0x00464955`, where a rebuilt thing's model handle reads empty; the cause is inferred). Delete the copy
    afterwards.
13. **Alexah's Full Simulation jungle park stalls at park tick 43**, its main thread busy in the track rides' stepper
    `FUN_0043ce20` (return addresses on its stack, sampled with `gdb -p` on the host): its stored matrices are readable,
    its clock never runs. Seen at 6.9 days of uptime; whether a restart cures it is not tested.

### What it is not

- **Not a Windows or macOS route.** On Windows the game runs natively, with a no-CD exe and the same clock bug. On
  macOS, CrossOver (Wine) is the nearest thing. Neither has been tried.
- **Not the retail `TP.exe`.** SafeDisc 1.41's driver will not start under Proton (`docs/exe/boot.md`). Every run
  uses a no-CD exe; the reference install uses `testme.exe`.
- **Not a player's install.** Players use `tools/play-the-original/` (the guide and `tpw-setup.sh`). The reference
  install was made with that same script, with `--exe testme.exe --no-menu`.

## The executable's own routines under unicorn

A leaf routine that touches no Windows API, no thread and no object built at run time can be run unchanged under the
unicorn CPU emulator, on the game's real data, without launching the game. Its output is the original's own, so it is
a bit-exact test oracle for a port. The fork review (2026-09-30) did this for the movie player (`docs/exe/boot.md`,
"The movie player"): the dequant table `FUN_006710c0`, the TQI macroblock decoder `FUN_006747d0` on all 9,412 frames
of the nine movies, and the stereo ADPCM `FUN_00672210` on every audio chunk. Q195 re-ran the table and the audio: the
table equals the formula read in Ghidra, and the audio equals ffmpeg's `adpcm_ea` sample for sample in all nine.

**How.** Map `testme.exe`'s headers and sections at its image base (`pefile`), map a stack, a scratch area for the
arguments, and a sentinel address holding `HLT`. Push the arguments and the sentinel as the return address, then
`emu_start( function, sentinel )`. The scripts are `emu.py` (video), `audio_emu.py` and `full.py` in the review's
`tqi/` folder, with their own venv (`CLAUDE.local.md`); a three-frame run takes under a second.

**Caveats.**
- **Set the x87 control word.** Code that rounds through the FPU depends on its precision. The movie runs used
  `0x027F` (53-bit, the Windows default); 64-bit gives the same pixels, 24-bit changes 37.5% of one frame's samples.
  Which one the original runs under is a measurement in the original, not an assumption.
- **Read the entry convention from the disassembly.** Many leaves take registers, not the stack
  (`FUN_006747d0`: `ESI`, `EBP`, `EDI`), and write into their caller's stack frame; the harness plays the caller.
- **Tables built at run time are not in the image.** They read as zero, so run their builder first (the colour tables
  of `FUN_00670350`), or the result is silently wrong.
- **A routine that calls the C runtime** (`__ftol` here) works, because that code is in the image too. One that calls
  an import does not, unless the harness stubs it.
