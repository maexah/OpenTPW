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
| `lib/parkrun.py` | The scaffold of every save-and-load confirm run (it sets `OPENTPW_LOG_DIR` to the run's `OUT` unless the script has, so the game's `console.log` pair lands in the run's folder and never rotates the game folder's own; Q647): a private game folder, `enter()`, `save()`, `load()`, `reply()`, `gaps( pattern )`, `shot()`, `predict()` and `said()`, `done()`; `OFFSCREEN = True` for a run beside the desktop's, on `:78` or `OFFSCREEN_DISPLAY` (a two-lane run's lane B takes `:79` and `GAME_FOLDER = "~/.cache/tpw-harnesses/lib/game-off-B"`); `GAME_FOLDER = "<path>"` for a game folder of the script's own (`lib/gamefolder.py`); `AUDIO_FILE = "<path>"` writes the game's own mix to a file, nothing to the speakers; `original_view( x, y )` for a saved camera the original's frame will show the point in |
| `lib/gamefolder.py` | A private game folder whose data differs from the real game's in the files named: `make( dest, { "data/Advisor/Advisor.sam": change } )` links everything else and writes a changed copy of each file (`once( old, new )` is a change that fails unless `old` is there once); hand the folder to `parkrun.py` as `GAME_FOLDER`, on the desktop or off it. For a balance key changed in this game without touching `/home/alex/Games/TPWorld` (Q338) |
| `lib/seedfile.py <in> <out> <old> <new>` | A copy of a park file with its `mRandomSeed` written over (the one place in the inflated block holding `<old>`, hex; the stock park's and `q340/late30.TPWS`'s is `3f416bba`), the block packed again: a different draw at the original's summons (Q583: seed 1 makes the ferry there). The original's draw is its whole state, not the seed's first, so try seeds |
| `lib/bindtables.py` | Every row of the original's six key tables (`FUN_0040cb80`: system, game, camera, cheat, coaster, shortcuts), read from `testme.exe`: action, key (`0x2600` an extended key's VK shifted, a lower-case letter a keypad VK), modifier, name and handler; then the letters bound and free with no modifier, for a key OpenTPW gives a job of its own (Q631) |
| `lib/mixmatch.py` | One sample measured in OpenTPW's own mix (`AUDIO_FILE`): `template( sdt, index )` decodes a bank's entry, `window( file, from, to )` cuts the capture between two sizes, `match()` is the best match and its least-squares gain, `steady()` the gain of a looped sample under other sounds, from 0.3 s pieces in 2 s steps, `pitched( t, pitch )` the template at a voice's drawn pitch in 96ths of an octave (a voice off pitch nought matches only so; Q612) |
| `lib/parkscene.py` | exec'd after `parkrun.py`: the one-line census readers (`var`, `thing_line`, `staff_line`, `bumper_line`, `field`), the Hot Pot scenes (`hot_pot`, `in_a_go`, `broken_pot`), an advisor tag's rect and the window point to click it from `tags`' reply (`tag_rect`, `tag_click`; Q415), and the pointer's: `pointer()` and `pick()`'s lines, `at( fx, fy )` a share of the window, `find_person( want )` the pointer stepped round the middle until `pick` names a person (a locator's camera keeps its person there), `ctrl( k )` (Q451), a person's pick circle in window pixels, `circle( thing )` and every drawn person's `circles()` (the console's `circle`, Q468; its middle to a tenth of a pixel since Q628), `wait_log( pattern )` for a game line that reaches run.log a moment late (Q468), `find_cell( (x, y), sx, sy )`, the window pixel near a start that `pick` answers with a named map cell, the hover's own (Q471, Q478), and a screen's own points: `layout_point( frame, rect, vx, vy )` a point of the 2048 x 1536 layout in window pixels through its frame control's place, `press_layout( ... )` a real click there, `drag( p0, p1, what, pressed=, held= )` a real drag between two window points (a slider's thumb; Q590), with `pressed()` run once the button is down and `held()` once it has moved, before the release (a census while a thumb is held; Q705), so a confirm script does not write them again. Its `at` and `field` are its own: a script that defines either after the exec breaks the helpers that use them |
| `lib/stockscene.py <outdir> <bin> <dest.TPWS> <TICK> [command ...]` | A scene file made from the stock Lost Kingdom park, off-screen: entered, paused on the first tick at or past TICK, each console command given in turn (`sell 13`, `buy 1101 41 22 0`), the rides, shops and sideshows listed and the park saved to `<dest.TPWS>`. A stamp or a flag is then written into a copy with `parkfile.thing_set` |
| `lib/pathscene.py <outdir> <bin> <dest.TPWS> <TICK> <N> [X0 Y0 W [H]] [-- command ...]` | A scene file of the stock Lost Kingdom park with more developed cells, off-screen: paused on the first tick at or past TICK, `path X Y` for the first N cells of the W x H block at (X0,Y0) (default 19 x 20 at (60,15), bare grass; its column 79 holds track cells), each with a 0.15 s wait, then each command after `--` (`fire 28`, the guard), saved: 126 + N developed (`parkfile.py -d`). Count the file, not the replies; never run it beside `mutate.py` (late replies cross) |
| `lib/cellprobe.py` | exec'd after `parkrun.py`: the window point over a named map cell, found by moving the real pointer and reading `strip`'s drawn census while a tool shows one square under it (`find( (x, y), start )`, `probe()`, `drawn()`, `squares()`), so a real click lands on a cell the original's run used (Q433); with the path or queue tool ANCHORED, `strip_end_at( (x, y), start )` finds the point whose strip ends on a cell in line with the anchor, by stepping along the axis, and works under the locator's moving camera (Q540); its step signs assume the unrotated camera, and it cannot find an end on the anchor's other axis (it stops when the strip ends on neither of the target's lines) |
| `lib/cropgrid.py IMG CX CY HALFW HALFH OUT [--scale 8] [--step 10] [--mark Y,...] [--markx X,...]` | A frame's few pixels made countable: the box cut, enlarged without smoothing, a red rule every STEP frame pixels labelled with the frame's own coordinates, and a cyan line at each predicted row and column, to read a sprite's drawn top, bottom and edges against the marks in either game's frame (Q470, `q470/confirm.py`) |
| `lib/bothcam.py X,Z ZOOM ROT [PID]` | One camera in both games, by the original's numbers (`camera.py`'s point, zoom and rotation in degrees): `console()` is this game's `camera X Z ZOOM -ROT` (its yaw is the original's rotation negated), `ours( reply, ... )` sends it from a confirm script after `parkrun.py`, `original( pid, ... )` runs `setcam.py` with the follow stopped, `both()` does the two; the command line prints the console line and, given a PID, sets the original. At the same numbers this game's 1280 x 720 pixel is (640, 360) + (the original's - (512, 384)) x 720 / 768 (Q440, Q441); `to_original( x, y )` is the way back (Q478) |
| `lib/parkfile.py` | The one reader of a park file: `voices( p )` (the sound manager's voice list after the sound block: the category slots, the pool's blocks and each voice's handle, class, place, range, category, effect, variation and sample places, controllers and child; `effect_id( cat, index )` names the effect from its SFX map; Q560), `script_objects( p )` (every script's object list: type, handle, node, lookup, tag; Q560), `-N <file>...` prints the voices, each with the script record, the crowd's word or the rain's handle that keeps it; `coasters( p )` (the coasters module: the four counts, the first coaster's header, its partners and every piece record, cell on the 96-wide grid, loft, bank, incline, yaw, loop; `parkfile.py -K <file>...` prints it; Q593), `analyser_grid( p )` (the analyser's `+16` to `+62598`: the five grid dwords, the 1,024 blocks with their nine value and worked-out pairs, the four small rings, the six kept answers) and `sound( p )` (the sound block's three dwords, its length and whether it ends at `NUOS`; Q546; `q546/sweep.py` sweeps both over a list of files), `cellflags( p, x, y )` (a cell's mType and mFlags: `0x20` NOMODIFY, `0x40` outside the park; `parkfile.py -n <file>...` prints every NOMODIFY cell with its neighbours' type and flags; Q538), `cells( p )`, `hoarding( p, x, y )` and `show_map( p, X0, Y0, X1, Y1 )` (every cell's map type and track record, whether a cell is a hoarding, a character map of the cells by type, the hoardings marked; `parkfile.py -c <file> [X0 Y0 X1 Y1]`; Q469), `object_days( p, thing )` (an object's customers ring, State of repair and remaining life, and the all-items row's customers and bars of them, Q367; and its shops' and sideshows' columns: the takings less the costs rings' last thirty, the totals' difference, the satisfaction and its bar, Q368; `-o` prints every object's), `advisor()` (the advisor thing's message records not fresh; Q313), `advisor_forget( src, dst, message )` (a copy of a jungle file whose advisor has never said a message, its block packed again; Q316), `advisor_slots()` and `advisor_clocks()` (his eight slots, the messages waiting to be said, and his last action, kept message and message playing; `-a` prints all three; Q314), `clock()` (the clock module's two dwords, the clock and the second stopwatch; `-a` says how much of the advisor's wait the file was saved with; Q315), `Park` (things, cells, sprites), `track()` (the track-rides module, cars and riders), `models()` (every model record: item, flags, node words, lookup records, channels), `head()` (a sprite), `effects( p, i )` (a cell's five region-effect words) and `tickets( p )` (the golden tickets' cell measures: Global 3's covered cells and the Secret's watched per cent; `-T` prints them; Q591), `emitters()`, `emitter_words()` and `emitter_aim()` (the particles module's live emitters by their chains, where one is not its template, and its own velocity at `+0x38`; `parkfile.py -e <file>` prints them), `particle_density()` (the module's density, the live header's `+0x20`, which a park read from the file runs at; Q703), `tags()` (the interface block that ends the body: each aerial tag's id, text, life, picture, buttons, thing and item, with the bytes the original leaves unwritten; `parkfile.py -t <file>...` prints them), `thing_set( src, dst, thing, offset, fmt, value )` (a copy of a file with one field of one thing's record written over and the block packed again, answering the old value: how a state no screen of either game reaches is put in a file; Q325), `economy( p )` and `economy_set( src, dst, fields, loans )` (the economy thing, thing 8: the fee, balance, batch, withdrawals, last balance, red stamp and profit and the eight loans' eight fields each, read, or written over by name and packed again: a bought loan no screen of this game buys; `parkfile.py -E <file>...` prints them; Q524), `calendar( p )` (the park clock's five fields, the 28 bytes before the arrival block, with the date of the file's tick and whether the month and day are that date's, as every file the original saves past tick 0 holds; `parkfile.py -C <file>...` prints them; `q526/clocksweep.py <list>` runs it over a list of files and counts the original's apart; Q526), `weather( p )` (the weather thing, thing 7, every field by the FILE's offsets: the quality, the change's and the forecast's stamps, the forecast, the drops, the lightning, the thunder's count, the bolt's location or None for the debug fill, the voice's handle, the wind; `_tick_of( ft )` a stamp as a tick of the file's clock; `parkfile.py -W <file>...` prints them; `weather_set( src, dst, fields )` writes fields over by name and packs it again; `q528/wsweep.py <list>` counts the original's files' stamps, thunder and location; Q528), `analyser()` and `filetime( tick )` (the park analyser's last 76 bytes by the serialiser's names, its six calendar stamps among them, `mLastStaffHired` at the record's `+73524`, and the calendar on a tick as such a stamp; `parkfile.py -s <file>...` prints them beside the staff's newest `mTimeHired`; Q333), `sets()` (the message centre's 29 listener sets, who hears each message type, as a load puts them back; `parkfile.py -x <file>...` prints them; Q397), `vehicles()` (the arrival vehicles: the header's three slots and current vehicle, the object list, cell (0,0)'s chain and each slot's thing's record by field and raw; `parkfile.py -v <file>...` prints them; Q342), `lab()` (the research lab's record: the used projects, the five kinds' records, `mNumProjects`, `mWorkLoad`, `mMostExpensiveProject`, `mLabActive`; `parkfile.py -l <file>...` prints them, a `restart.INTS` as well; Q349), `staff_patrols()` (every member's patrol corners, `+476` and `+478`, with the whole map and no area named; `parkfile.py -p <file>...` prints them; Q380), `pool()` (the staff pool's 32 slots by field; `parkfile.py -P <file>...` prints those in use; Q422), `pool_set( src, dst, kind, valid=0 )` (a copy with `mValid` written on every slot of that kind: a pool with nobody of a kind to hire, which the advisor's row 17 asks for; Q422) and `ring_set( src, dst, [(thing, ring, days, today)] )` (a copy with objects' day rings written over, `OBJECT_RINGS` by name (`costs`, `takings`, `customers`, `walkaways`, `served`, `satisfaction`), the days newest first; a shop's walk-away share or satisfaction for the advisor's rows 238 to 257; Q713), `money_rings()` (the analyser's eight month money rings, the bank's balance at each close (no month so far), all
costs, staff costs, training, cash in, gate, shops' and sideshows' takings: slot, length, wrapped, the month so far, the newest
and the closed months, from the record's `+62598`, 589 bytes a ring; `parkfile.py -m <file>...` prints them; Q385,
Q386, Q387), `park_value( p, prices )` (the park value as `FUN_004c7db0` sums it from the file alone: the cells by type
and track record, the objects by their tiers' costs and repair; with `itemcosts.prices()` it is the analyser's 2nd ring's
figure; `parkfile.py -w <file>...` prints the cells and objects; Q388); `money_rings` also answers the 2nd ring as
`value` and the 11th, the lifetime visitors at each close, as `visits`, beside `analyser()`'s `mLifetimeVisitors` (Q389), and the 10th and 12th, the guests inside and the rides standing at each close, as `inside` and `rides` (Q390), and the 13th, the guests the arrival vehicles dropped, with its month so far, as `arrived` (Q391), and the 14th, the mean happiness of the guests inside at each close, its months read as FLOATS, as `happy` (Q392), and the 15th, the mean of the last fifty leavers' stays at each close, as `stay`, beside `stays()` (the ring of fifty stays itself at the record's `+72022`, oldest first, and the mean the close would push; `-m` prints it as `leavers`; Q393), and the 16th, the park score out of 100 at each close, as `score` (Q394; `q394/sweep.py`'s `score()` and `facts()` reckon it from a file); `loads()` (the arrival manager's ring of five loads at the record's `+72235`, oldest first, with how many are called and the raw five, `0xcdcdcdcd` where none reached; `-m` prints it as `loads`; Q396); `developed()` (the map's developed cells as `FUN_004c81b0` counts them, every cell of `mType` 1, 3, 4, 9 or 10 inside the park or out, with the cells by type; `parkfile.py -d <file>...` prints them; 126 in the shipped jungle park; Q411) and `bare_blocks( p, w, h )` (every block of bare grass inside the park, nearest a path first: where a scene can lay path); `guests_inside( p, below=30 )` (the guests the analyser counts inside, model 1 on a cell of `mType` 0, 1, 3, 9 or 10, and those under `below` happiness, the record's `+422` cut to its low byte, as the advisor's row 14 asks; with the entertainers; `parkfile.py -g <file>...` prints them with the share and the crowd; Q415). `litter()` (the litter share as the advisor's `FUN_004ccdf0` takes it: the littered cells over the cells of path, queue or type 9 or littered; `parkfile.py -L <file>...` prints it with the littered cells) and `litter_set( src, dst, cells, kind )` (a copy with `mLitter` written on each cell, the sprite handle left nought, so neither game draws it; Q417). `coverage( p, role=4 )` (the advisor's role coverage as `0x004cd500` takes it: the mechanics' (4) entrances or the handymen's (5) path, queue and entrance cells inside any of their patrol rectangles, staff with the whole map not counted; `parkfile.py -V <file>...` prints both; Alexah's jungle `New Save` 25 of 71, share 35, as the original read; Q602). `boundary( p )` (the boundary's anchors, track type 25: each track sub-record's `mMeshInstance`, `mTileData` and
`mHoardingNeighbours` against the model record it names, and its `mDirection` and `mFlags`; sorted by `mesh` the anchors
are a fresh park's walk, which `q656/model.py` runs the decoded piece chooser over; `parkfile.py -B <file>...` prints the
census; Q620, Q656). Add a module's reader here, not in an item's folder |
| `lib/itemcosts.py [theme...]` | Every item's three `Upgrades[k].CostOfUpgrade` from the game's own `.sam` files (`wadcat --cat .sam` over each theme's wads), `{Info.Id: [tier 0, 1, 2]}`, cached in `itemcosts.json` beside it; `prices()` answers it (Q388) |
| `lib/uitypes.py` | Every item's `Info.WhichUIType` across the four themes, `{Info.Id: kind}` (0 rides and upgrades, 1 shops, 2 sideshows, 3 features, 4 the items no buy list shows): the folder's category unless the item's own `.sam` sets one, cached in `uitypes.json` beside it; `types()` answers it. A park file's rides, as the analyser's `FUN_004c8070` counts them, are its model-3 things of kind 0 (`q390/sweep.py`; Q390) |
| `lib/ringsweep.py [<list of files>]` | The park analyser's sixteen int rings across every park file on hand (it finds them, or takes a list): per ring, the files holding it on the cash-in ring's slot, those with a month so far, the commonest first months; then the balance law (each closed month of the 1st = the one before + the 6th's month - the 3rd's) and every file that breaks either (Q387); then the visitors law (the 11th never falls, its newest at most
`mLifetimeVisitors`, no month so far) and the files that break it (Q389). One file's rings: `parkfile.money_rings` |
| `lib/fsplayer.py` | exec'd AFTER `parkrun.py`: the file `fsplayer.py` is exec'd from reads the private player's `gms.dat` bytes (the backup `fs_off()` writes back), and `fs_on()` puts its mode to Full Simulation (`gmsfile.put`, mode 0), so `enter()` loads the stock park (`easymode.TPWI`, the newest file) as Full Simulation, the catalogue's 70 items and no `Easy_` layer; `fs_off()`, after `done()` in `finally`, puts the file back byte for byte. `fs_profile( path )` replaces the private player's `gms.dat` by a copy of another profile (Alexah's Full Simulation one, or a copy `gmsfile.put` edited), so the run plays as that player: their tickets, keys and records (Q591, `q706/confirm.py`). A file saved so loads in the original only through `original/fsload.sh` (Q580) |
| `lib/freshpark.py` | exec'd before `parkrun.py` with `FRESH_GAME = "<folder>"`: a game folder of the run's own whose only player has no `gms.dat`, so `enter()` makes a **fresh Full Simulation park** (no park file, 70 items, tick nought) in place of the stock Instant Action one. Pause straight after `enter()`; `hire` and `buy` work under pause (Q349) |
| `lib/labsim.py <park file> [--ability 12] [--researchers 1] [--first 760] [--tech 0,0,80,85,85] [--fpu 53\|24]` | The original's research lab run forward from a park file's own lab (`parkfile.lab`), by `park-engine.md`, "The lab at work": one line per project finished, its hand-out, tick, kind, item, a group raised and the kind's next project, then whether all are done. `--fpu 53` (the default) is the sweep's measured precision: the share in doubles, the double sum compared before the float store; `24` the old float model. Stock Easymode: Dino Karts on hand-out 95, tick 2640, the last on 1177, tick 24280 (Q245), the same under both; `ParkLabTests` pins all 24 against OpenTPW's lab (Q701). The ticks are floors: both games' researcher rests (Dino Karts on tick 2920 in the original, 2980 here). Not modelled: an appended upgrade (none in Lost Kingdom's Easy layer), a resting researcher, hires, `ResearchTech[5..]` |
| `original/freshfs.sh make <outdir>` / `stop` | A fresh Full Simulation park made by the ORIGINAL, off-screen: a player folder with Alexah's Full Simulation `gms.dat` and no park file, picked in the lobby; copies the `restart.INTS` the original writes and leaves it running in that park for a memory reader. `POLL='python3 -u advisor.py %PID% 330 0xb9 poll'` starts a reader just before the player's click, so it sees the park from tick nought (`<outdir>/fresh.log`); `GRAB=N` takes N frames, one each `GRAB_EVERY` s (default 5). Run whole three times (Q327). `GMS=<gms.dat>` puts that profile in place of Alexah's (Q591; `fsload.sh` passes it through) |
| `original/fsload.sh <file> <outdir>` | A park file loaded in the original by a **Full Simulation** player (the game type is the player's, and `loadfile.sh`'s `1ref` is Instant Action's): `freshfs.sh make`, the file put in that player's jungle folder, its own Load list's first row clicked, `advisor.py` (`MSGS=`) and `tick.py` from just before the click, a frame every 2 s for 80 s, then `freshfs.sh stop`. Run as `q327/orig/run-load.sh` (Q327). `POLL='...'` replaces its two readers as `loadfile.sh`'s does (`object.py`, `loadframes.py` with `JUMP=1`), and `GRABS=N` frames, one each ~2 s, set how long the original runs before the stop (35; `q580fs/orig/run.sh`, Q580). **Look at its `1-load-list.png`**: twice in Q590 it never loaded (a "hire researchers" box was already over the park, which swallowed Escape and the clicks, and once the list opened empty); then load by hand: `freshfs.sh make`, the file copied into `save/users/2fsfresh/jungle`, Escape, Load (510, 50), the file's row (400, 123 the first, 143 the second), `freshfs.sh stop` after (`q705/orig/d`). **`freshfs.sh` runs `POLL` too**, at its own make, a minute before the load, so readers started there write the same files twice: arm them on the second call (`POLL='if [ -e $D/armed ]; then ...; else touch $D/armed; fi'`, Q595) |
| `lib/sfxmap.py <data dir> [<cat_..SFX.map> [effect]]` | A sound category's effects and variation headers (flags, parameter, volume, pitch, wait, range), walked as the loader walks them; one effect's variations each with its sample records named (bank, entry from 0, the name its `.sdt` stores, length), the bank found from the BANK file beside it, case-blind (Q632); with the data folder alone, the spread of every variation's range over all 31 files. Importable (`Category`, `categories`, `banks`, `sdt`, `names`) |
| `lib/soundsweep.py [THEME] [all]` | Every script's `ADDOBJ`/`EVENT` sound of types 3 to 9 over the `.RSE` corpus: its effect's flags (its category's SFX map), its class and each variation's sample lengths in ms; without `all` the one-shots and the ids a category lacks. Lost Kingdom: 198 sounds. Q565 |
| `lib/responses.py [ID ...]` | The advisor's response table read from `testme.exe` on disk (610 rows: sample, lip file, gesture, model slot and bank, the two props, group): the spread of slots and props, or the rows named. Importable (`rows()`, `off( va )`, which turns any address of the image into a file offset) |
| `lib/advisorrow.py MESSAGE...` | An advisor message's metadata row (`0x0076e300 + id x 0x38`, read from the emulated fill `q177/skeptic-analyser/meta_full.json`): posted only or polled, category, game mode, score function, copies, first response and lines, then each line's response row. Importable, `row( id )`. Q141 |
| `lib/gmsfile.py <gms.dat>...` | A player profile's progress (`saves.md`, "gms.dat"): the four record and two secret ticket flags, spent, keys given outright, each theme's six park tickets and four records and its last two bytes (`renamed`, `all_researched`, Q550), the ticket-bought ride ids, and the tickets and keys counted as the game counts them. Importable, `read( path )`; `put( path, mode=, all_researched=, tickets=, records=, park_records= )` rewrites the mode byte, records' all-researched bytes, each named theme's six park tickets (`{theme: [6 flags]}`), the four player-wide record tickets (`[4 flags]`) and a theme's records (`{theme: {k: (held, value)}}`) in place, which made a Full Simulation player of the private Instant Action one (Q550), took tickets away so a park wins them again (Q591; write a copy) and cleared or moved a record so a park wins it (Q706: `q706c/gms-R1.dat`, `gms-R4.dat`); `spent=` and `ride_ids=` set the tickets spent and the ticket-bought rides (Q709: `q709/gms-two.dat`, 2 of 4 left and none bought). What the original awarded: Alexah's Full Simulation profile reads jungle Locals 0 to 2 and record 0 at 106. Q141 |
| `lib/advisorschema.py [OFFSET ... \| all]` | The advisor's `.sam` schema walked from `testme.exe` on disk: the `Advisor.sam` key behind each offset of the balance object `0x00fb3540`, which is `ECX` in every score function of the advisor's metadata table (`[ECX + 0x5e4]` is `TutorialUIPanel.Score`). The array's four extra bytes are anchored on three known offsets, not read. Importable (`walk()`) |
| `lib/tagtable.py [ID ... \| groups]` | The tag table read from `testme.exe` on disk (382 rows: id, key, picture, string, the bar's two buttons, life, text kind): the spread of each field, the rows named, or every response group with its row. The tag a said advisor line leaves on the gadget's aerial is the row of its response's group. Importable (`rows()`, `find( id )`) |
| `lib/hidelists.py <advisor.wad> <stem>` | Every clip's hide list of a model in a wad (`<stem>m1.md2` on), each node by name: what a clip's start hides |
| `lib/cliptracks.py <wad> <stem> <suffix>...` | Each track of a model's clips (`<stem><suffix>.md2`: `Advisor_JUN m19`, `Fries C`): its node by name, flags and descriptor words, and a scale track's keys (frame, x, y, z) (Q597) |
| `lib/cliphide.py <data dir> [path filter]` | Every shipped clip whose visibility keys leave a MESH node hidden at its end (index under the model's mesh count), by model, and how many of the model's clips name that node in their hide list: what a change of clip shows again and hides (`LobbyModel.ShownAtChange`; Q622: 239 clips in the game; Lost Kingdom's are the three vehicles' take-offs, the End's planes, the advisor's and nine placed things' clips). Run it with `review-codex/venv/bin/python3` from `lib/` |
| `lib/scalescan.py <data dir> [subdir...]` | Every clip carrying a scale track (`0x80\|0x100`), by wad, with the count in each: 644 tracks in 192 clips game-wide, 128 in 54 under `levels/jungle` (Q597) |
| `lib/scalekeys.py <data dir>` | Every scale track's keys game-wide: the first key's frame, the last key against the clip's declared end, the pad word, the order, and the channels sharing the track (644; 623 start at 0, 632 end on the declared end) (Q599) |
| `lib/ticketbox.py <png> [interface-left-px]` | The advisor's big golden ticket in a frame: the box of the largest near-white run (its border), in the frame's height, and its middle from his anchor (0.8 across and down the 4:3 interface; this game's is pinned to the window's right edge, the original's frame is the interface, pass 0; on a window wider than 4:3 this game draws the grown ticket toward the middle, Q639, so read its middle against the window's there). Run with `review-codex/venv` (numpy, PIL). Where the gate's white doors touch the border the height reads high (Q599) |
| `lib/md2surf.py <wad> <member.md2> [x z]` / `--sweep <data dir>` / `--uvs <data dir>` | A model's header float, lookup records and the faces a point stands on, with the height under a model point; the sweep counts them over every wad. Importable (`Model`): a reader of a `.md2`'s header, lookup table, meshes, vertices and faces to build a model check on; each mesh's `.uvs` (one per vertex-order entry) and `uv_runs( model )`, and `--uvs` lists every material run whose UVs leave 0..1, which `q363/wrapflags.py` sets beside its frame's wrap bits (Q363) |
| `lib/twinfaces.py <wad> <member.md2>` / `--sweep <data dir>` | A model's faces drawn twice: face records on the same three points (each group, its face numbers, the normal words they name, material, whether turned opposite ways), or every `.md2`'s count, a line a mesh. The original draws only the one wound to the eye (`render-states.md`, "The software back-face test"). Importable, `twins( model, mesh )` (Q586) |
| `lib/normdump/` (`dotnet bin/Debug/net10.0/normdump.dll <data dir> <path in data> <out.json>`) | A model's meshes as this game's reader reads them, to JSON: each mesh's name, vertex-order length, faces' normal indices, the `+0x64` normal table and the normals OpenTPW computes (`ModelFile`). Build it once in its folder (it references the main clone's `OpenTPW.Files`). Q657: the advisor's |
| `lib/normcmp.py <orig.json> <ours.json>` | The original's live normals (`original/advlight.py --json`) against `normdump`'s, mesh by mesh: the table's entry for each vertex-order entry (0.0000 apart on all 43 meshes of the global and Lost Kingdom advisors, Q657) and the angle to OpenTPW's computed ones (FileFormats `models.md`, "Normals") |
| `lib/uiwinding.py <ui.wad> [<member.md2>]` | The UI faces the original's face test drops: each mesh's faces through its node chain, kept when they turn clockwise in the file's (x, y) (`render-states.md`, "Which draws the test covers"); the sweep prints a line a mesh with any turned away, then the totals; one model lists the faces. Importable: `winding`, `placed`, `turned`. `q608/coverage.py` and `mask.py` raster one mesh's turned-away area on it. Q608 |
| `lib/walkriders/` (a project: `dotnet lib/walkriders/bin/Debug/net10.0/walkriders.dll`) `<data dir> <park file>...` / `--objects <park file>` / `--nodes <item dir> <stem> <cellX> <cellY> <angle> <space hex> <id>...` | (It references the main clone's build, so lane B of a two-lane run does not build it.) Every walk slot in use in the park files beside its rider's saved sprite (place, facing, set, script, or none) and the placer's rule for it, tallied; the walkers' sprite facing against the octant of their angle; `--objects` each object's thing, item, cell and turn; `--nodes` a model's nodes as `RideNodes` composes them at rest, with their facings. In this game the `guests` census says the same per person: "WALK at (x,y,z) facing f" or "WALK hides" (Q585) |
| `lib/parkrun-smoke.py <out> <bin> [off]` | The scaffold's own check: enters Lost Kingdom, saves under pause, loads the file back from the Load Park list and compares the three ticks; `off` runs it on the private display |
| `lib/uistream.py <VA>` | A compiled UI layout stream of `testme.exe` on disk walked to its balanced close (`hud.md`, "The opcodes"): every control's type, flags, id and rect, its mesh hash, text rect, help row and outline, indented by its parent, each close printed. It does not read ops 9 and 10 (a list's scrollbar): a stream with a list stops there |
| `meshhash.py <hash> ...` | A layout stream's mesh hashes turned back into the `ui.wad` model to load, by file stem or by a model's first node name (`w_park` is the node `window3`) |
| `lib/wctpng.py <wad> <member> <out.png>` | One `.wct` texture out of a wad as this game's reader decodes it (`wct2raw`), a PNG and an 8x copy, with its header's version, size and scales (it prints offset 10 as "Cb" and 12 as "Cr": offset 10 is Cr's, `exe/textures.md`); the member matched by the end of its path (`'textures\\ridestatbar.wct'`). The colours are the reader's, the original's codec since Q355 |
| `lib/gaugesplit.py <frame.png> <x0> <y0> <x1> <y1> [<meter.png>]` | Where the gadget's happiness gauge stands in a frame of either game: each row of the meter's box scored against `meter.wct`'s lit and dark half at that height, the best split as a share of the height (comparable with the meter's percentage), and the rows' verdicts (one D run then one L run when the box is right). The box: ours `control 1f`, the original's `(42,550)-(72,662)` of 1024 x 768. Its fit, an rms RGB distance, is 37 to 88 on real frames, so judge by the verdicts (Q244) |
| `lib/wctorig.py <file.wct\|file.sgn> [out.png]` | A type `0x12` `.wct`, or a `.sgn`'s artwork, decoded the ORIGINAL's way (`FUN_00593ab0`, `FUN_00592730`; `exe/textures.md`): the reference to check `TextureFile` against (983 of the 986 zlib files within 1, Q355). Writes an RGBA PNG and an 8x copy. Takes the alpha chunk by its size, which in every shipped file is the bit count's rule (FileFormats `texture.md`). A sign comes out in the codec's order: the original's board holds it with red and blue exchanged (Q357). LZSS (`0x13`) is not done (Q354) |
| `wct2raw/` (a project: `dotnet wct2raw/bin/Debug/net10.0/wct2raw.dll`) `<in.wct\|in.sgn\|in.md2> <out.rgba>` / `--batch <list> <outdir>` | `.wct` textures or a sign's artwork through the repo's own `TextureFile`/`SignFile`: width, height, RGBA. `--batch` decodes every path in a list in parallel to `<outdir>/<line>.rgba` (8,396 in 6 s), failures on stderr; given a `.md2`, each mesh's materials with their flag words; copy the project beside a worktree of main (its `ProjectReference`) for the control (`q355/wct2raw-main`) |
| `lib/wctcensus.py <dir>` | Every `.wct` under a folder (`wadcat --dump .wct` run inside one folder a wad, as names repeat across wads: `q355/alldump`, 8,375, and its `loose/`, the 41 loose files' 21 names; `all.list`) counted by type, colour codec, alpha byte, bit count and alpha chunk, an example of each. `q355/compare.py` is the decode against `wctorig.py` and main to copy |
| `lib/alphauses.py <materials.txt> [<dump> <wads.txt>]` | Every model material of the jungle's wads, `lobby.wad` and `ui.wad` naming a `.wct` whose alpha chunk sits under a header byte 1 of nought (the original draws it cut out), split by the material's `0x2` bit, the one this game's world shader lets alpha through on. `materials.txt` is `wct2raw <model.md2>` over a model dump, `q358/materials.txt` the 751 (Q358) |
| `lib/matword.py <data dir> [--list SUBSTR]` | The material record's last word (`+0x0C`) over every static `.md2`, wad members and loose files: the census of its values, the uses with bit `0x1` (drawn unlit, white, by `DrawList_AddMaterialFaces`) and a line a mesh holding one; `--list` prints every material of the models whose path holds SUBSTR, with its frame word. 847 models (841 under `data/`), 12,960 uses, 929 unlit in 656 meshes; in the jungle's `base.MD2` the river's ten water materials (Q619) |
| `lib/seam.py <frame.png> x0 x1 y0 y1` | A dark seam between water and its near bank, column by column in a window: the share of seam columns, the median ratio of the darkest pixel below the water to the water's and the bank's, and the longest run of neighbouring seam columns. Compares frames of two games lit differently, where a fixed level does not (Q358) |
| `lib/screendiff.py <a.png> <b.png> [x0 y0 x1 y1]` | Pixels differing by more than 40 between two frames inside a box, and where: a screen that should not change, checked against the control's frame (Q358) |
| `lib/rectfilm.py X Y W H SECS OUTDIR [--display :0] [--min 4] [--keep]` | Films one rectangle of a display (root `get_image`, screen pixels) as fast as it reads, about 20,000 times a second for 140 x 56, and times each change of its picture: `changes.tsv` (wall clock, gap in ms, mean difference, which distinct picture), the first of each picture as `pic-NNN.png`, `samples.txt` (the reads and the reads a second); prints the reads a second, the changes a second, the gaps' 10 ms histogram and the gaps in order. For an animation's drawn rhythm in either game on the desktop, the original's at its 30 fps cap included. Q640 |
| `lib/scrollphase.py FILMDIR PERIOD [--axis x\|y] [--from S] [--to S] [--hann] [--move R]` | How far a repeating texture has scrolled in each picture of a `rectfilm.py` film: each picture's line of brightness cut to whole repeats of PERIOD (screen pixels), the phase of its component there, unwrapped; prints the speed in repeats a second (+ to larger x), the RMS and largest distance from a straight line, the steps, the gaps over 30 ms, the stand (the time to move R repeats, default 1/62) and the residual's three strongest frequencies beside the pixel rate (speed x PERIOD); writes `scroll.tsv`. A stepped animation reads as a staircase with a hold (the research pump in the original: +0.411, steps of 1/31, its residual's peak at 0.40 Hz, once a loop); a smooth scroll as a line whose rest ripples once a screen pixel crossed, so under a pixel the stand measures the grid, not a hold (Q677 rounds A, B). Q677 |
| `q642/ours.py <out> <bin> <ROUND> [X,Y,W,H]`, `q642/orig/run.sh OUTDIR [RECT]` | A model's drawn rhythm in each game: the stock park at one camera, `rectfilm.py --min 1.5` on a moving part (ours: also paused), the original at `ORIG_FPS=60`; kept in the item's folder, not `lib/`. `run.sh` defaults to the 420 x 340 rectangle that read too slowly: pass one. The recipe is "A model's drawn rhythm in both games". Q642 |
| `lib/diffpic.py <a.png> <b.png> <out.png> [x0 y0 x1 y1]` | Two frames side by side and a third, the second dimmed with every pixel differing by more than 40 a channel in red: where a change lies and its shape, and the count (Q359) |
| `lib/shadowdiff.py ON.png OFF.png CX CY [HALF] [--scale S] [--near X,Y] [--out crop.png]` | A person's shadow from a frame pair of either game, shadows drawn and not, all else equal: the pixels darker by more than 6, with `--near` only the connected group nearest a point (one person's), their box from a point (the person's place pixel; `--scale 1.0667` puts ours on the original's 768), the median per-channel ratio on/off (the darkening, 0.596 at medium in both) and an 8x crop. Ours: `shadows 0` / `shadows 1` under `pause`; the original: `memhold.py PID 4 0x008bcbc8:I=<word with 0x20000>` under the held clock (Q480, `q480/orig/run.sh`) |
| `lib/patchmean.py IMG NAME=X0,Y0,X1,Y1 ... [--vs IMG2]` | Named pixel boxes' mean colour, spread and brightness in a frame of either game, and with `--vs` the same boxes in a second frame, the difference and `mad`, the per-pixel mean absolute difference (how much moved where the means agree, Q486); `mean( img, box )` and `mad( img, img2, box )` importable. A colour compared across the two games or two runs at one camera (Q479) |
| `lib/watermean.py IMG [X0,Y0,X1,Y1]` | The mean colour, brightness and count of a frame's water pixels (green and blue each over red by 40, blue over 0.6 of green: the river's cyan), so two games' water at one camera is compared over the whole river, where a box lands on different parts of an animated surface (Q652: one box 0.73, the river 1.11). Not for a frame whose grass reads cyan |
| `lib/sharp.py IMG NAME=X0,Y0,X1,Y1 ...` | How sharp each named pixel box of a frame is: the mean absolute 4-neighbour Laplacian of its luminance, with its mean colour, and the first box over the second. Compares a crisp cell with a blurred one in one frame, so two games at two resolutions compare by their ratios (Q617: the gate's scenery strip against the ground, 4.97 in the original and 13.41 here; the Belly Bounce's floor against the ground, 1.33 and 9.79) |
| `lib/lndtile.py <base.lnd> <N> <x> <y> [out.png]`, `... sheet X0 Y0 X1 Y1 out.png` | The original's procedural ground tile made by its OWN code, `FUN_0055f780` run under unicorn on the file (`Tiler( path, N ).tile( x, y )`, `.rgb()`): the bit-exact oracle for a port (Q483; `park-engine.md`, "The procedural ground"). `tile( x, y, rec )` with rec = (texels, w, h, depth, word) runs the path laid over too, the texels as the original's surface holds them (Q489). Needs `~/.cache/tpw-emu312/bin/python` |
| `lib/pathoracle.py 16\|32` | The expected values of OpenTPW's path-laid-over tests: the stock park's 78 path tiles and the word and grey-cell cases, hashed after the original's `FUN_0055f780` (through `lib/lndtile.py`) at tile size N with each path texture as the original's surface holds it (Q489 for 16, Q618 for 32; `ProceduralGroundTests`). Reads `q483/lnd` and `q489/` for the base.lnd and the path textures. Needs `~/.cache/tpw-emu312/bin/python` |
| `lib/exeleaf.py` (import: `Exe()`, `alloc`, `write`, `read`, `call( fn, *args, ecx= )`, `reads`, `signed`) | A stack-argument or thiscall leaf routine of `testme.exe` (set other registers through `x.mu`) run unchanged under unicorn on memory the caller lays out: the image mapped at its base, a stack, a heap, a `HLT` sentinel, FPCW `0x027F`; `call` answers EAX, `reads` lists every address outside the stack a call read, globals and heap. The oracle for a port's arithmetic (Q704's `q704/fill.py` runs `Lab_PercentLeftOfDearest` on a made-up lab and on a park's, `fslab.py`). `lndtile.py` keeps its own copy of the set-up. Needs `~/.cache/tpw-emu312/bin/python` |
| `lib/lndsheet.py <base.lnd> <N> <out.rgb> [--paths CELLS RGBADIR]` | Every procedural ground tile of a park (cells 0..95 x 0..84) by `lndtile.py`, in one file, and its SHA-256: what OpenTPW's `ProceduralGroundTests` hold `ProceduralGround` to (Q488). `--paths` lays a path over each cell CELLS names ('X Y NAME WORD', NAME a `wct2raw` `.rgba` in RGBADIR), as the original's 16-bit surface holds it (Q489; `q489/paths.cells` is the shipped park's 78). Read back `numpy.fromfile( f, uint8 ).reshape( 85, 96, N, N, 3 )[y, x]`. Needs `~/.cache/tpw-emu312/bin/python` |
| `lib/cellmatch.py AIM SHEET X0,X1,Y0,Y1 FRAME...` | Which procedural ground tile each game draws on a cell: a homography from the original's `camera.py --aim` pixels of named cells (`q488/orig/run.sh` writes them) cuts each cell out of a frame of either game at one camera (this game's mapped as `bothcam.py` says) and matches it against `lndsheet.py`'s tile of the cell and its eight neighbours, with the cells' mean colour. The original at Q479's camera A: its own tile on 84 of 122 cells (Q488). `ONLY=FILE` takes only the cells a file names; `PAIR=FRAME0` instead correlates each cell's luminance with the same cell in another frame, game against game (Q489: on path cells, where neighbours look alike and the tile match cannot tell them apart) |
| `lib/barshare.py <png> <x0> <y0> <x1> <y1> ...` | A type-9 bar measured in a frame of either game, by its layout rectangle: the share of its middle row in colour, the share grey, the colour's mean r - g, the last coloured pixel, the grey's mean, and the pixel an eighth of the way. **A black cell counts as grey** (spread under 12): judge "nothing drawn" by the grey's mean, under 5 there and 49 to 58 on a drawn track (Q367). **It reads a
`happygrad.wct` bar about 0.02 short** (its greyish-teal texels spread 30 or less): use `barsplit.py` for that one (Q369) |
| `lib/barsplit.py <png> <x0> <y0> <x1> <y1> ...` | A type-9 bar's fill by where its grey track starts (the first pixel of the middle row, from the left, with a spread of 12 or less and a mean of 40 to 75), as a share of the width: right for any ramp, `happygrad.wct`'s too, and blind to a highlighted row's band past the bar's end (Q369). **A lettered number's anti-aliased edge passes its track test**: where a cell may hold text, not a bar, judge "no bar" by `listcells.colour` (Q371) |
| `lib/listcells.py <png> <y0> <y1> <x0,x1> ...` | A list row's cells in a frame of either game, virtual units: `aligns( png, y0, y1, cols )` where each column's white lettering stands in the row band (start, end, middle as shares of the column, and `left`/`centre`/`right`), `bars( png, ys, x0, x1, h )` `barsplit.py` down a column of bars, `colour( png, ys, x0, x1, h )` each bar rect's middle row in colour (spread over 60: a ramp is, lettering is not). The visitors list's alignments in both games (Q371); `ink( png, x0, x1, y0, y1, white )` the bands of lettering down one column and `pitch( bands )` the median step between them: a list's row height and its font's size in either game's frame (use a column with `$` or digits and white 110; Q377: a name's descender, the `j` of "Rajan", drops its band's middle 4 units and moved a three-row pitch to 49, Q378). **Pass `white=110` to `aligns` and `ink` on this game's 1280 x 720 frames**: at the default 150 a thin `2` reads None and `$ 0` only its brightest pixels, ending 0.86 of a right-aligned column (Q383). **Judge a figure from the right by its END, common to every row**, not its middle: the original's "$ 2500" stands its middle at 0.72 of the buy list's price column and ends at 0.951 on every row (Q384) |
| `lib/texmatch.py <png> <x0> <y0> <x1> <y1> <texture.png>...` | Which of a set of textures a frame of either game draws in a cell, virtual units: the box round the cell's light pixels, each texture's opaque box stretched onto it, scored by the mean colour difference on the texture's darker (icon) pixels; `match()` answers the best, its score and the next best's, or None for no picture. **Read the margin**: a thought's thumbs-up and thumbs-down differ by 2 to 4 in the original's frames, every other pair by 8 or more. The visitors' thought cells in both games, the textures from `wctpng.py` (Q373) |
| `lib/rectcolour.py <png> <x0> <y0> <x1> <y1> ...` | What fills a framed control in a frame of either game, by its layout rectangle (its outer 4 per cent left out): the shares of yellow lettering, white, teal (`!cbut`'s body) and black, and the mean colour, a line a rectangle. A heading framed or bare, its lettering yellow or white (Q370); a screen's title (Q676) |
| `lib/screentext.py <outdir> <bin> [WxH]` | The buy and hire screens and every tab of each, opened by real clicks in the stock park at a window size (2560x1366 asked by default, which the desk makes 2552 x 1355), a frame a tab, and every text's `UI text:` census line (`OPENTPW_TEXT_CENSUS=1`: its drawn size, its box, the size it was drawn at, `OVER` past its box) in `census-<tab>.txt`, each tab's count and overs printed (Q644) |
| `lib/mutate.py <mutations.py>` | Putting an item's bugs back, in parallel, in copies of the tree (`WORKFLOW.md`, "Verifying"); a two-lane run's lane B passes `--tree ~/repos/OpenTPW-laneB --root ~/.cache/dn-tmp/mutate-B`, so its copies are not lane A's |
| `lib/capped.sh [-m SIZE] CMD...` | Runs CMD in its own systemd user scope capped at 6 GB with no swap (exit 137 when killed at the cap), so a test that runs away dies alone and the machine never runs out of memory; `mutate.py` and `gate.sh` run every `dotnet test` through it. A full test run with the game peaks at 1.5 GB |
| `~/.local/bin/claude-rc` | Starts Claude's remote control in its own scope with `OOMPolicy=continue`: a terminal's own scope has `OOMPolicy=stop`, so one process killed for memory takes Claude and every session with it |
| `lib/watchdog.sh` | One read-only look at the machine's health, every minute by the user timer `tpw-watchdog.timer`: low memory, a process over 8 GB, `/tmp` (RAM) over 70%, `/home` under 30 GB, a zombie over 2 min, a game, test or debugger over 3 h, one busy 30 min. One line per new problem and one when it clears, in `watchdog/alerts.log`; `watchdog/wait-alert.sh` blocks until a new line |
| `lib/nvwatch.py [--keep] [--quiet]` | Has NeoVeldrid released a version newer than ours? Reads every `NeoVeldrid*` `PackageReference` on main (git objects), asks NuGet's public flat container for each package's newest stable version (a plain GET, nothing about the user sent), and prints `pinned X, newest Y`, and `NEW <package> <version>` for one never announced (`nvwatch/state.json`); a `NEW` line also goes to `nvwatch/alerts.log` (the Sentinel's `watch-child.sh` shows it) and to the desktop by `notify-send`. Run every Monday by the user timer `tpw-nvwatch.timer` with `--keep` (each version announced once; weekly at Alexah's word, 2026-10-26) |
| `lib/testme.objdump` | The whole of `objdump -d -M intel` of `testme.exe` (50 MB): grep it rather than dump the executable again |
| `lib/gdump.sh [--print] <out-dir> <addr>...` | Every address's function in one Ghidra call: `<entry>.asm` (disassembly, calls named), `<entry>.c` (decompile), `<entry>.xrefs` (who refers to it), `loose_<addr>.asm` for an address in no function, `index.txt`. Runs on the private copy (`gd run_python`), whose names lag the real project until it is refreshed; `--print` prints the code for one `mcp__ghidra__run_python` call on the real project. Grep the files instead of one tool call per function per kind |
| `lib/citecheck.py <sha> [--repo DIR] [--all]` | Every executable address a commit's added lines cite, checked with no model call: a code address starts an instruction (objdump, then Ghidra for what objdump doubts, in one call), a data address falls in .rdata or .data, and an address beside a function's name lies in that function (`lib/funcnames.tsv`). Prints MID-INSTRUCTION, NO-INSTRUCTION, NAME-MISMATCH, NAME-NOT-IN-COPY or OUTSIDE with file:line; `mkev.sh` puts it in every review's evidence. A hex value that is not an address (a four-letter tag) shows as NO-INSTRUCTION |
| `lib/funcnames.sh` | Writes `lib/funcnames.tsv` (entry, end, name of every function) from the private Ghidra copy; run it after the copy is refreshed from the real project, so `citecheck.py` reads our names |
| `lib/ffmerge.sh <repo> <branch> [master]` | Fast-forwards the main branch (`master` in FileFormats) to a finished branch and deletes it: checks out the main branch first, refuses a dirty tree or a branch that needs a rebase. The gate script `audit-2026-10-17/gate.sh` has a dry run for after an edit: `GATE_DRY=1 gate.sh main` prints what it would use and "dry: OK" |
| `lib/stryker.sh <project> <File.cs>...` | Mechanical bugs in a file, Stryker.NET |
| `handoff/` (`README.md`, `prompt.md`, `wait.sh`, `mkev.sh`) | A coordinated queue run (Alexah's word only, CLAUDE.md rule 20): a coordinating session starts one session per item with `create_session`, whose first prompt is `prompt.md`'s `/goal` (under 4,000 characters; a slash command sent later arrives as plain text), `wait.sh <status file> <s> [quiet min]` wakes it when the item ends or goes quiet, and `mkev.sh <out> <sha> <qN>` builds one item's review evidence for a `tpw-verify` reviewer. The README holds the lessons (2026-10-16) |
| `original/loadfile.sh load <file> <outdir> <tag>` / `stop` | A park file loaded in the original from its Load Park list, off-screen, with frames; `POLL=` starts a memory reader just before the click. It starts the original only when it is not running, so a second file is 35 s where the first is 123 s |
| `original/usedlist.py [--pid N] once` / `watch SECS` / `free [N]` | The original's thing numbering, live: the used list from its head (`id:model`), the free list's first ids and the deleted list; `watch` prints each id made or freed with the free list after it, a `POLL` for `loadfile.sh` (Q582). Imports `tpwmem.py`, which since Q582 runs its `main()` only as a script. This game's own is the console's `thingids [N]`, and `thingfill [N]` takes free ids, held by no thing, until N are left, so a full table is met in a small park (Q610) |
| `original/loadframes.py PID SECS X,Z ZOOM ROT OUTPREFIX [EVERY]` | Frames of the original at a camera of your own from the moment a Load Park click's file is read in: a `POLL` for `loadfile.sh` that waits for `mGameTick` to fall, writes the camera with `setcam.py` until it stays (the load puts its file's camera back over its first frames), and takes `OUTPREFIX-t<tick>.png` every EVERY s (1.5). `RESET="8.5,10,12"` writes the camera again that many seconds after the fall: **`loadfile.sh`'s align sweep, 6 s after the click, drags the pointer to the edges and pans the camera** (Q26c, `q26c/orig3/run.sh`). `JUMP=1` takes a rise of more than 50 ticks as the load too: under `fsload.sh` the fresh park's clock stands below the file's (Q580). X,Z `-` writes no camera (the file's own kept, ZOOM and ROT ignored): frames named by the tick alone (Q743) |
| `original/screenpark.sh <file> <outdir> <tag>` | A park file entered by the original on the REAL screen (`VERIFYING.md` rule 128: off-screen draws no sign), for a picture: the file made the newest in the reference player's jungle folder, the welcome and the player clicked, frames `<tag>-1..3` at 1024 x 768 and `-N-full` at 1920 x 1440 from 35 s on, then the original stopped, the file taken out and `save/` checked. 2.5 minutes a file; the camera is the file's own (Q357). Its two player clicks are 3 s apart: at 1.2 s the second fell inside the lobby's island flight at 1024 x 768 and the park was never entered (Q377) |
| `original/lobbypanel.sh <outdir>` | The original on the REAL screen at the lobby's island panel for a Full Simulation player (a copy of Alexah's `gms.dat`, keys 2, as player `2fsfresh`, the second row): the slot clicked once, frames `a-NN` every 2 s (the panel from about `a-04`), then a second click that enters the park (`b-NN`); the player folder removed and `save/` checked. Ours at the same panel: `q608/confirm.py` (`fs_profile`, the slot's click, `control 1e0ea`/`1e0eb`, `freeze`). Q608. `FILM=SECS` films the game's picture from about 2 s before the entering click (2.06 s in `q630/orig-a`) (`entry.mkv`, 1024 x 768 at 30 a second; `click.t` and `entry.t0` the wall clocks) in place of the `b-NN` stills, and `POLL='cmd %PID%'` runs beside it from about 4 s before the click (the film starts 2 s after the poll, the click 2 s after the film) (`poll.txt`; `emitters.py %PID% 20` for the key puff). `START_WAIT=SECS` waits that long after the start before the welcome click (default 40; the intro once outlasted it, `q670/orig-a`). Q630, Q670 |
| `lib/lobbyentry.py <outdir> <bin>` | Ours entering Lost Kingdom from the lobby as a Full Simulation player (a copy of Alexah's profile, keys 2), on the desktop: the island panel, its price (`control 1e0ea`), a frame `before`, a real Return, then frames `f-NN` every 0.05 s for 2 s and every 0.3 s after (`FOR=` seconds, default 2) with their means, `particles screen` (the on-screen effects, each with its owner window and whether it is drawn) every 0.1 s for 2 s and `windows` at 0.5 s; the load waited for and the profile put back. `CANCEL=<s>` presses Escape that long after the Return, asks `windows` 0.2 s later and waits for no load (Q669). Beside `original/lobbypanel.sh` with `FILM` and `POLL`. `ORBIT=<radians>` sets the orbit by the console's `orbit` just before the Return (about 1.2 s, 0.24 radians, before it: subtract that to start the turn at a measured angle), and the "Lobby camera" lines (the leave's start and turn, facing the gate, arrival, each with seconds and passes) are printed (Q670). Q630, Q669, Q670 |
| `original/lobbycam.py PID [SECS] [POLL_MS]` | The original's island-lobby camera, a line a pass: finds the object by its vtable `0x00702ec0` in the mappings under 2 GB (about 0.2 s), then state `+0x14` (0 orbit, 1 turn, 2 fly-in), angle `+0x18`, radius `+0x20`, vertical `+0x24`, the delta `[0x00f82884]+8`, the fade word `[0x00f83778]+0x60` and the island's heading; its first line the wall clock of its start (against `lobbypanel.sh`'s `click.t`), its last a summary per state (passes, seconds, passes a second, median delta, angle turned, radius). A `POLL` for `lobbypanel.sh` (`POLL='python3 .../lobbycam.py %PID% 22'`). Q670 |
| `original/resolution.sh 800\|1024\|640\|...\|off\|show` | The reference original's resolution, by writing `Data/Resolution.sam` (the disc's `_Resolution.sam` renamed turns the override on; `off` removes it and the Options screen's 640 x 480 rules). **It stands at 800 x 600** (Q377). Run it with the original stopped; check with `tpwmem.py read 00faa5c0:h` |
| `original/groundcell.py PID SECS X,Y[:X2,Y2]=+MASK\|-MASK\|t@X,Y ...` | The original's memory, WRITTEN: the park ground's heightfield cell words (flags low, texture index high) ORed, cleared or given another cell's texture, held; prints the base model's and the terrain pass's heightfields (the same one), the render word `[0x008bcbc8]`, the size and each write. `HF=engine` writes the terrain pass's own pointer, `READ=X,Y[:X2,Y2]` prints those cells' words first, `AFTER=FILE` holds the writes until a frame is taken. A `POLL` for `loadfile.sh`, or run beside a park already loaded. The ground is rebuilt every frame: `+0x1` opens a black hole on the next one (Q360) |
| `original/heads.py PID SECS ITEM` | The original's memory: every model of an item with its lookup state (shared flags, heads attached, each record holding one) and each head's sprite, place, scale and frame, a line a change. A `POLL` for `loadfile.sh` |
| `original/emitters.py PID SECS` | The original's memory: the particle system's live emitters by the used chain (slot, count, handle, effect, place, aim, life, particles alive, the four rates and the most as the density left them), a block a change headed by the live density (`[0x0080cef0]`, the loaded park file's), and the particle counts a second. A `POLL` for `loadfile.sh`. The place it prints is the start's units over 1024 (the emitter keeps them over 16, read over 64): an on-screen effect started at the gadget's tag, (5810, 0, 45947), reads (5, 0, 44) (Q638) |
| `original/scriptvar.py PID THING [INDEX [VALUE]]` | A ride script's variables in the running original, by thing: read, or **written** (the one tool that writes its memory). It is how a ride is broken on demand |
| `original/object.py PID SECS THING [VAR ...]` | The original's memory: one object's `mState`, `mCanLoad`, the guest being loaded, the queue's head, the member assigned, the State of repair and the remaining life, and those variables of its script, a line a change with `mGameTick`. A `POLL` for `loadfile.sh` |
| `original/retrigger.py PID SECS THING [TIMES]` | Sends an arrival vehicle round again in the original as a summons does: each time its script's `VAR_STATUS` (variable 1) reads 0 after anything else, writes its `VAR_TRIGGER` (variable 0) 1, TIMES times; prints each status with `mGameTick`. A `POLL` for `loadfile.sh`; it WRITES the original's memory (Q622, `q622/orig/run.sh`) |
| `original/money.py PID SECS THING...` | The original's memory: each object's all-items columns, its customers last thirty days, last month's profit, total profit, satisfaction and bar, worked out from its takings, costs, customers and satisfaction rings and its two totals as the original's getters do, a line a change with `mGameTick`. A `POLL` for `loadfile.sh` (Q368) |
| `original/tick.py PID SECS` | The original's memory: `mGameTick` every 50 ms, a line a change, a fall marked `FALL`: the moment a Load Park click's file is read in, and the tick it starts at. Run it beside a reader that prints only on its own changes (`POLL="python3 -u tick.py %PID% 25 & python3 -u inside.py %PID% 25; wait"`, Q241j). A `POLL` for `loadfile.sh` |
| `original/waitdate.py PID MONTH DAY [SECS]` / `PID now` | The original's memory: waits until its published calendar (`0x007ced58`) reads that date and prints the date and `mGameTick`, exit 1 after SECS (default 120); `now` prints today's. A day is 23.04 ticks, about 5.7 s, and a month about 3 minutes of the stock park (1 Mar to 1 Apr took 177 s), as its park sweeps about four ticks a second: wait minutes, not seconds. Made for Q641 |
| `original/thingwords.py PID SECS THING:OFFSET[:FMT[:NAME]] ...` | The original's memory: any words of any things, a line a change with `mGameTick`. Use it for a field or two before writing a poller: the mechanics' search cursor is `2:0xc`, a mechanic's state, job and count `26:0x19c:I`, `26:0x218`, `26:0x214:I`. A `POLL` for `loadfile.sh` |
| `original/spritepass.py PID`, `particleheights.py PID SECS EFFECT [STEP]`, `particleoverlay.py PID OUT.png [EFFECT]` | The original's world particles: the sprite pass's matrix (`[0x0087b088]`) with every emitter's draw words and its particles projected to pixels; how high and wide an effect's particles go over a time; and a frame grabbed with each particle's sprite marked on it, which proves a projection rule against the picture |
| `original/camera.py PID [X,Y;...]`, `--aim X,Y` | The original's park camera now: the point, zoom and rotation, the camera's matrix (its last row the eye) and the draw matrix, and named cells' corners at 1.5 and 21.5 put through it to pixels of the 1024 x 768 frame; `--aim` prints the pixel of a cell's middle, so a run's clicks follow whatever camera it has (Q435) |
| `original/lens.py PID [SECS]` | The original's lens now: the angle handed to the projection builder, its near and far, the projection, the screen scale, the lobby render camera's own angle and `gui_CameraFlags`, then the picture's angles top to bottom and across; with SECS a line a change (Q260) |
| `original/camto.py PID X,Y [TOL]` | The original's park camera panned to a cell by resting the pointer on the 1024 x 768 frame's edges (its arrow keys turn the view), each edge's sense and rate learnt as it goes; with Mike's window opened before, the camera stayed where it was put for the 20 s measured (Q439) |
| `original/cellfind.py PID X,Y [PX,PY]` | The original's side of `lib/cellprobe.py`: the pixel over a map cell, found by moving the real pointer and reading the build preview's cell (`[0x008186b8]`, `[0x008186b4]`), Newton steps from `camera.py --aim`'s pixel (Q439). The preview reads (0, 0) under a park screen, and also with no tool armed on a park just loaded by `loadfile.sh` (Q635's run a: every probe (0, 0)): `PICK=1` reads the picker's cell `[0x007b05cc]` instead, which a member's window leaves live and which follows the pointer with or without a tool. Its probes stay inside 0.95 of the way to each edge, past which the pointer pans the camera. With the camera still and no panel or object mesh under the pixel, `camera.py --aim`'s pixel was the cell on the first probe, six of six at rotations 0, 90 and 180 (Q440) |
| `original/spritepick.py PID SECS [census]`, `spritepick.py PID where THING [depth]`, `spritepick.py PID census` | The original's sprite pick, read-only (a `POLL` for `loadfile.sh`): a line a change with the pointer (raw and as -1 to 1), the latch `[0x008bd4fc]` and its owner, the latch the decode PREDICTS from every sprite carrying `0x200` (`park-engine.md`, "The thing under a sprite"), the pick's answer and the hover's person as thing ids, the cell, and the latched sprite's d2, r2, w, size and flags; `census` lists the sprite list by flags and owner kind once. `where THING`: that thing's circle's middle as a pixel of the 1024 x 768 frame (aim the pointer with it), its w, and the draw matrix times its place beside its stored place (Q468); `depth` adds its picture's texture flags and state word, the pull `Sprite_BuildQuad` gives its z, the buffer's z either side and the pull in eye units (Q477). `PID census` alone: the sprite list now, by flags and owner kind of any kind (a person's claim to a script first: a manager's `+0xc` can equal a person's script index), with the sizes and scales and whether `0x200` is set (Q470) |
| `original/staffpair.py PID SECS` | Waits until two of the stock park's staff (things 25 to 30) are drawn with their pick circles' middles within 40 x 30 px of each other, both well inside the 1024 x 768 frame, and prints the pair and both `spritepick.py where` lines; exit 1 after SECS. Hold the clock straight after (`memhold.py PID S 0x007859a0:I=1`): the two stand still and their circles with them (Q628, `q628/orig/sweep.sh`) |
| `original/cellhead.py PID [THING ...] [@X,Y ...]` | The original's cell lists, read once: the cell a thing is linked into (its bytes `+5`, `+7`) with its address (what `[0x007cc308]` and `[0x007b05ac]` hold), and a cell's chain head first (the cell's `+0x24`, then each thing's `mMapChild` `+0xa`), with `mGameTick`. Here the console's `cell X Y` (`occupant`) and `staff` (`cell`) (Q446) |
| `original/hoverscan.py PID CATS [person\|noperson\|any] [click] [pick] [grid X0,X1,DX,Y0,Y1,DY]` | The original's hover found by stepping the pointer (xdotool jumps on `$DISPLAY`, default `:77`, over 1024 x 768 points): after each jump the category `[0x007c24f4]`, the cell `[0x007b05cc]` and the sprite pick's person `[0x007b05ac]` as id and kind; it stops at the first point of a category in CATS with a person there, not there or either, and with `click` presses at once and reads what the click keeps, the person `[0x007c2650]` and the object `[0x007c2654]` (Q474, `q474/orig/run.sh`). With `pick` (Q494) each line also gives the picker's point P `[0x007a1c70]` (x, height, z) and the model record the object march met `[0x007a1cd4]` as its handle (0: the march met nothing; set, it met that record, whichever of its point and the ground's became P). Exits 1 when no point matched: run it again for walkers to move |
| `original/setcam.py PID X,Z ZOOM ROT [off\|on]`, `setcam.py PID off\|on` | The original's park camera WRITTEN: the point (world units), the zoom and the rotation in degrees, the rotation asked for (`0x00790a38`) and, setcam's own choice, the drawn one (`0x007909e0`; the game's snap writes only the first), and the scroll terms as the snap writes them, so the next frame is drawn there; `off` stops the follow first, `on` starts it on the locator's person. Reads back the point and the eye against the formula; exit 2 where the point did not stay (a follow or a pan moved it) (Q440) |
| `original/campoi.py PID [SECS [INTERVAL]]` | The original's park camera point `0x007908f0`/`0x007908f8` and scroll terms `0x00790a88`/`0x00790a90` (units a ms) polled, with the zoom, the asked rotation and the key words, after one line of the heightfield's counts, sizes and the edges the camera's soft bound uses (960 by 850 in Lost Kingdom; `park-engine.md`, "The point of interest's edge", Q625); since Q626 also the drawn rotation `0x007909e0` and the copy first person saves and puts back, the point `0x00790ad0`/`0x00790ad8` and the rotation `0x00790a9c` (`park-engine.md`, "Entering and leaving first person"). `SECS` 0 prints one line. Read-only |
| `original/camrate.py LOG FIELD` | The rate a `campoi.py` log's `zoom` or `rot` moves while it moves: a least-squares line a millisecond through the polls between its first and last change, the first and last 0.1 s dropped, or the changes themselves where it jumps (a quarter turn); and how far the point moved (Q631) |
| `original/mapview.py PID [SECS [STEP]]` | The original's park map object (the static `0x00f86db8`): its centre across and down `+0x54`/`+0x58` (cells), zoom `+0x5c`, the heightfield's cells `+0x24`/`+0x28`, the picture's size, the pointer, the right-drag flag, the layer bits and the two radios, beside the camera's look-at `0x007908f0`/`f8` and the point the map opens from, `0x00790ab8`/`c0`; with SECS a line every STEP seconds (a held arrow's rate) (Q624) |
| `original/mapgrid.py PID [grid\|records\|all\|hover]` | The original's map overlay while the map is open (`hud.md`, "The map's overlay"): its three words (layers `+0x18`, staff `+0x1c`, metric `+0x20`), the code grid's count per code with each thing's code and box and the land codes' boxes, and every sprite record (sheet, kind, filter mode and value, scale, shown, cell and screen point, the thing and a member's patrol) (Q664); `hover`, the hovered record, the cell's thing id word `+0x20064` and the text the label `0x991` is given for them, worked out by the script from the decoded rules (a member's `+0x1a0`, UITEXT 456 and 457, the word's joined name), the frame being the label's own evidence (Q665) |
| `original/diffcells.py A.png B.png` | Where two frames of the original's map differ, as boxes of cells (calibrated on the stock park's map at zoom 1 on (48,26), 800 x 600 in a 1024 x 768 frame: x = 455 + (cx - 39) x 6.4, y = 365 + (29 - cy) x 6.4); with `original/maprects.py SHOT...`, the blue patrol outline's box (Q664) |
| `original/mapptr.py PID` | The original's map pointer as its last move over the map left it (`+0x10`/`+0x14`), the centre, zoom and screen, the cell the hover's own sum puts under it with that cell's code and id, and the word `+0x20064` beside it: whether a hover's aim landed where it was meant (Q665) |
| `original/maptarget.py GRID WHAT` | A frame point (1024 x 768, the original at 800 x 600) to glide to on the open map, from a `mapgrid.py` dump: `staff` the shown member's dot, `guest` the shown guest dot farthest from every other, `thing:N`, `cell:X,Y` a cell's middle (Q665). The glide lands about 3 pixels up and left of it: aim 3 past |
| `original/outside.py PID [X,Y] [N]` | The original's map cells outside the park (`mFlags`, the word at `+0xe` of each 0x44-byte cell, bit `0x40`) nearest a cell or the camera's point, as runs along a row: where a refused rectangle of the patrol tool lies (Q439) |
| `original/capmatch.py CAPTURE SDT ENTRY FROM TO ...`, `... --scan FROM [SECS]` | One sample of a bank measured in the original's own mix (an `ORIG_ASOUND` file capture, its byte marks taken with `stat -c %s` either side of what is measured): each window's score, gain and rms; `--scan` scores every 2 s window to the end, so a click's window can be shown the capture's only match (Q442) |
| `original/particledump.py PID EFFECT [raw]` | The original's memory: EVERY particle of each live emitter of an effect (its place less the emitter's, velocity, size, frame, colour) and the spread of each, where `spritepass.py` prints an emitter's first six. OpenTPW's side is the console's `particles <slot>`; `q261/cloud.py <run.out>` prints the same spread from a run's output |
| `lib/spritesheet.py <bank.ESP> <out.png> <set>...` | A sprite bank's picture sets with their alpha, each frame over black and over white, and each frame's alpha spread and mean colour: what a particle's picture is before any blend. `wadcat --dump SPR_PA.ESP` and `--dump SPR_PA.TPC` on `esprites.wad` write the two files it reads. `spritesheet.py <bank.TPC>... --find W H OX OY`: the pictures of that size and origin, with the columns and rows their alpha reaches and those of alpha 128 or more over the origin's row: a figure's own edges, to predict a frame's from (Q475; Mike's are `Generic\Mechanics\SPR_OM`) |
| `original/voices.py PID [SECS]` | The original's memory: every live sound voice by the service's own list (its effect and flags word, the voice's flags, place, range and rectangle, how far it is from the listener, and at the end its handle and its class by vtable as a park file's list numbers it, Q560), with the listener first, its line ending with the crowd's handle word `[0x00803aa4]`; a block a change. Says which placed sounds are in range and sounding. A `POLL` for `loadfile.sh` |
| `original/mixer.py PID [SECS]` | The original's memory, inside `QMixer.dll`: the session and its listener, and every open channel's flags, place, distance mapping, volume, the mixer's own distance, and its cone, distance and final gains, with the gain the law gives beside the one read. How loud the mixer is playing a voice, where `voices.py` says only whether it sounds. A `POLL` for `loadfile.sh` |
| `original/crowdmix.py PID SECS` | The original's memory: the crowd's voice (level, handle, keys, values, the current variation with its weight, the sample record) beside every flat mixer channel's volume of 127, in ONE log stamped with the wall clock and `mGameTick`, a line a change, 20 ms a poll. Use it where `crowd.py` and `mixer.py` would have to be held together by eye; `mixer.py` prints nothing for a change of volume alone. A `POLL` for `loadfile.sh` |
| `original/mixfreq.py PID SECS [2d]` | The original's memory, inside `QMixer.dll`: every open 3D channel's place, far end, volume of 127 and FREQUENCY (`+0x1f4`), a stamped line each time one opens or changes, and at the end a count by far end, volume and frequency. Which pitch each sample of an effect plays at; `2d` reads the flat channels instead. A `POLL` for `loadfile.sh` |
| `original/chains.py PID SECS [POLL_MS]` | The original's memory: every chain voice (effect flags `0x4` without `0x2`: the held chain `0x404` and the chain of any variation `0x4`) by the service's own list, a line when one comes or goes and when it makes a child (its time word, the gap to the one before, sample record, variation, channel), and at the end per effect the children, the distinct samples and the gaps' spread. Since Q559 each child's variation as `v<k>` (its place among the effect's from 1), the chain's key, parameter and own variation as it comes, and per effect the variations' counts and the repeats of the one before. A chain out of range (`0x800`) was seen making none (89, 71 to 73; a pass ticks only a voice in range) and, first seen at `0x802`, making some (177): the flags are printed at first sight only, so measure. The place reads (x, height, z). A read made part-way through a draw can name a variation its sample is not of: take a child's variation from `brk.sh` at `0x006c3aba` where it matters. Read-only (Q558) |
| `original/screenchains.sh` | The original on the REAL screen entering its reference player's newest jungle file, or `FILE=` a park file put there as the newest, with `chains.py` from the moment its pid exists; `BRK=ADDR[,ADDR]` (`BRK_PRINT`, `BRK_HITS`) starts `brk.sh` once the lobby is up, the island clicked until the gates' chain 177 comes (`WAITFOR=`), `mixer.py` after, the camera put on `CAM=X,Z` (turned by `ROT=`, run h's 180) and three frames; then stopped and `save/` checked, or `KEEP=1` left in the park. `OUT=` the folder, `TAG=`, `SECS=` (Q559). `EXTRA="<reader> [args];..."` more readers from the pid on and `LATE=` more beside `mixer.py`, each given PID and its seconds before its args, into `<TAG>-<reader>.log`; `SHOTS="t1 t2 ..."` the frames at those seconds after the pid (Q561); `LOBBY_WAIT=` the seconds before its first click (40; Q560's run a lost the player row's click at 40, run b entered at 70) |
| `original/musicvoices.py PID SECS [EFFECTS] [POLL_MS]` | The original's memory: every voice of effect flags `0x4` and `0x2` (the music's class, the plain class) by the service's own list, a line when one comes, goes or changes its variation (`v<k>`), sample record, channel, flags (`0x40` parked, `0x800` out of range), controller keys and values or place; at the end each voice's order of variations. `EFFECTS` a comma list of ids (Q561) |
| `original/oneshots.py PID SECS [POLL_MS]` | The original's memory: every one-shot voice (effect flags without `0x4`, looped under `0x8`) by the service's own list, a line when one comes (its sample record, the record's length, its drop word `+0x1c`), changes its flags or channel, and goes (its life, whether it ever played); at the end per effect the life against length + 250 ms and against the drop word. A walk can tear while the sound thread changes the list (old voices flagged `0x10` "come" and "go" within one poll). Q565. In one 25 s walk it saw no effect flagged `0x1` (29 of the ui category's 45, `0x0201`, and one hallow ride's), which goes through a deferred queue first (`audio.md`, the voice class table): two clicks' `0x65` went unseen (Q632); nothing in the script filters `0x1` out, and the cause is not shown (whether a deferred voice joins the walked list, and under a 10 ms poll) |
| `original/oneshots-phases.py LOG NEAR AWAY BACK END` | `oneshots.py`'s voices split by a run's line marks and by place (the toilets, the Jungle Spray at (525, 315), kids 208 beside it at (525, 300), the till of a guest paying on leaving it (Q567), the others): how many came and were played, and life - (length + 250); a torn read left out and counted. Q565 |
| `original/rainmix.py PID SECS` | The original's memory: the weather thing's quality, forecast, drops, target and its own rain handle, the global rain handle, the two rain voices (controller keys and values, the current variation's volume bytes) and every flat mixer channel under 40 of 127 with its frequency, in ONE stamped log, a line a change. How loud the rain is and what sets it; `crowdmix.py` is the same for the crowd's voice. A `POLL` for `loadfile.sh` |
| `original/advisor.py PID SECS MESSAGE[,MESSAGE...] [THING]` | The original's memory: the park advisor (the thing `FUN_005194d0` answers): each named message's own record (the tick last said, the line, said, slaps), the eight slots (subject thing, line asked for, message, score), the last action's start and duration, and a thing's State of repair beside them, a line a change with `mGameTick`. Says whether and when he was told a message and whether he said it. A last argument `poll` ends each line with the poll's cursor (`[0x00fb3540]`, the row of the metadata table the next tick starts from; Q316). A `POLL` for `loadfile.sh` |
| `original/loads.py PID SECS` | The original's memory: the park analyser's ring of the last five arrival loads (`+0x216d8`: slot, count, wrapped, the five loads) and the loads called as the advisor's row `0x119` counts them, beside the lifetime visitors, a line a change with `mGameTick`. A `POLL` for `freshfs.sh` or `loadfile.sh`; two readers in one `POLL` are `reader-1 ... & reader-2 ... > file` (Q708: the first load of four on tick 604) |
| `original/advisorline.py PID SECS` | The original's memory: the speaker itself (`Advisor_SayResponse`'s globals): busy, the line's model slot, sample and props, the voice's due time (`0` while a lead-in runs, `none` once started or for a row line with none), the clips' end and the cue from the line's start, the clip words, the next clip's index, the flags of the clip word handed on most recently (the clip that plays after the running one), UI particle channel 0's count and the lobby island panel's pointer, a line a change with `mGameTick`; the twenty clip lengths once at its start. Says which clips and gesture row a line plays, when its cue falls and whether it had a lead-in (Q595) |
| `original/sayas.sh RESPONSE[,RESPONSE...] [SECS]` | The original's next lines said as the responses given, in order: a `gdb` hardware breakpoint at `Advisor_SayResponse` (`0x00599050`) writes each over the argument of a call that will say a line (advisor on, not busy), so the line is that response's row of the table (sample, lip, gesture row, slot, props) while the message's record and tag stay the posted one's; a line a swap (`said 404 as 18: tick 265`). It WRITES the original's stack: never save a park under it. The original ticks about 4 a second while it is attached. This game's console `sayas N` does the same for its next line, the tag included (the posted response's group, `0x0059b7ba`). Q596 |
| `original/advisortable.py PID [meta_full.json]` | The original's memory: the advisor's metadata table as the running game holds it (the image holds zeros): the polled rows as ranges with their count, a few rows' places in a round, and every dword where it differs from the emulated table (two, both row 0's). A `POLL` for `loadfile.sh` |
| `original/bytehold.py PID SECS ADDR=VALUE [TICK ADDR=VALUE]` | The original's memory, **written**: holds one byte of the image at a value, and at another once `mGameTick` passes a tick, a line a write. The Options screen's Tutorial switch is `0x0078d90d`. Stop the original afterwards |
| `original/helpbar.py PID [SECS]` | The original's memory: the help bar's label shown or not, the help sink's switches and the control the pointer came onto last (the park's view among them), the hover's category and cell and the entry-price screen; with SECS a poll at about 110,000 reads a second, a line in ms a change, fast enough to see the hover's own steps (each zeroes the category for a moment). The text is read from the frame (`560x50+232+712` at 1024 x 768). Made for Q473 (`docs/exe/hud.md`, "The help bar holds one posted row"); since Q495 also the buy screen's poster: its item `[0x007cc1e8]`, the item last posted `[0x007cc1e0]`, the state `[0x0075513c]` and the screen `[0x007cc1f8]` (`hud.md`, "The buy screen's help rows"); since Q496 `cam`: the game type, the online visit's tree, control, state and thing (nought in every offline park), the current mode's vtable and `gui_CameraFlags` (`hud.md`, "The online visit's help rows"); since Q497 `fp`: the hover's on-the-view flag and thing, layer 1, the library's pointed control and the cursor id applied, in the poll too (`hud.md`, "The help bar in first person"). |
| `original/memhold.py PID SECS ADDR[:fmt]=VALUE ...` | The original's memory, **written**: holds static words of the image (`I i H h B f`, `tpwmem.py`'s kinds) at values for SECS, a line a rewrite, then puts each back as it was. The fog's start and end `0x008bd490:f`/`0x008bd494:f`, the compositor's word `0x008bd550` (Q479); the game clock's paused flag `0x007859a0:I=1` (`0x00785970` + 0x30, not a real pause) holds the park still, every person in one pose, while the frame goes on drawing (Q477) |
| `original/compstate.py PID`, `tilecap.sh OUTDIR [HITS] [SECS]`, `lndcmp.py <base.lnd> OUTDIR` | The original's procedural ground compositor: its state read once (the ready bit, the box, the cache's tile size and count, the lnd object), every tile it makes caught by `gdb` as it is made (move the camera while it runs), with a path tile's record and texels (`rec-*`, `tex-*`, Q489), and each caught tile against `lib/lndtile.py`'s (Q483) |
| `original/prepoke32.sh` (background, before `loadfile.sh load` starts the original), `poke32.py PID` | The original made to draw its procedural ground in 32-pixel tiles, as on the machines of its day: `dwTotalPhys` `[0x008bd520]` written 256 MB (and the texture memory 64 MB if it reads negative) once the boot has set it, before the first park entry makes the tile cache; a later poke changes nothing. `compstate.py` then reads tile size 32. The original's frame at OpenTPW's default size (Q618; `park-engine.md`, "The original at 32") |
| `original/waiting.py PID SECS` | The original's memory: the park analyser's count of guests waiting for a shut park to open (its `+0x1f0d4`, written each time the advisor's row 1 is scored past its two early ends, -999 before), beside the same count taken live (things of type 1 in state 3 on the two ticket booths' cells, by id), `mParkClosed` and every guest's state, a line a change with the wall clock and `mGameTick`. A `POLL` for `loadfile.sh` |
| `original/gadgetdate.sh OUTDIR TAG TICK...` | The original, off-screen, enters Lost Kingdom from the lobby (the reference player's newest jungle file, the stock one on tick 755) and grabs the frame as `mGameTick` first reaches each TICK, the tick read just before and after each grab, the gadget cropped (`TAG-TICK-gadget.png`; its date box is (80,530)-(200,560) of the 1024 x 768 frame); `tick.py` beside it; nothing saved, `save/` summed either side. The gadget's date at a tick (Q149; Q527 wants it) |
| `original/tags.py PID SECS` | The original's memory: the tags on the gadget's aerial (how many are held, each one's control rectangle on the 2048 x 1536 layout and its text, the mast's rectangle, the tag on the bar), a line a change with the wall clock and `mGameTick`. Says when a line leaves a tag, where it stands and when it goes. A `POLL` for `loadfile.sh` |
| `original/aerialpoll.py PID SECS` | The original's memory: the aerial's mast and each of its children (the red top first, then the tags), each one's rectangle and its window-animation stream (whether one plays, which, its row, the offset it has added), a line a change with the wall clock, polled every 5 ms: a row of a stream and its time, to set beside a `record.sh` film (`q637/orig/run.sh` runs both under `loadfile.sh`). Q637 |
| `original/staffpool.py PID SECS` | The original's memory: the staff pool, each slot in use as slot:kind/name row/grade (`[0x007cf83c]`, 32 records of 0x14 bytes), beside the hire screen's tab and whether it is up, a block a change with `mGameTick`. The hire list fills in slot order, so the first of a kind is the first handed over; it shows each timeout and top-up. Resolve a name row with `strdump`. A `POLL` for `loadfile.sh` (Q356) |
| `original/bank.py PID` | The original's money, read once: the bank balance, the analyser's staff costs this month, the hand's worker (its low word), the current mode, the staff window's kept member and `mGameTick`, one line. Read it either side of a click to measure what the click cost (Q432: a dismissal's wage, 161) |
| `original/screens.py PID SECS` | The original's memory: what the gadget's three category buttons are left on (`[0x007cb28c]`, `[0x007cb2a0]`, `[0x007cb290]`) and which park screen is up (the open screen `[0x007c24c8]`, the buy screen `[0x007cc1f8]`, the hire screen `[0x007ca308]`, each pointer printed, so a screen destroyed and built again shows) and the hire screen's tab (`[0x007ca30c]`, `hiretab`, 0 the cleaners' to 4 the scientists'), a line a change with the wall clock and `mGameTick`. Says what a click opened without a frame. A `POLL` for `loadfile.sh` |
| `original/bar.py PID SECS` | The original's memory: the message bar (its rectangle, shown bit, parent, its data's thing, button types, item and text, each child's rectangle), the tag on show and the one being put on show, the gadget's body with its children two deep, the arm's handle, and each tag's rectangle, shown bit, timer byte and whether a stream plays on it, the changed parts a change with the wall clock and `mGameTick`. Says what a click on a tag, on the bar or on the camcorder button does to the arm, step by step. A `POLL` for `loadfile.sh` |
| `original/uitree.py PID GLOBAL` | The original's memory: a UI tree walked from the control a global points at (the hire screen's `7ca308`, checked; any other tree's global unchecked), one line a control indented by depth: its rectangle (`+0xc`), the rectangle its skin is handed (`+0x30`, which `FUN_00485e30` insets), its text and skin pointers and the skin's vtable; a meter's skin (`0x006feeb8`) adds its kept rectangle, its value of 100 and its matrix's scales. Says where the original really draws a control, where the stream's rectangle does not. Made for Q636 (`hud.md`, "The skill bar `0x2482`") |
| `original/breakq.py PID SECS THING [all]` | The original's memory, **written**: breaks a ride (`VAR_BREAKSTAT`) each time it is open with a queue that has stood four seconds, so every break puts a queue out, a line a break. With `all` it first writes nought over the mask byte at `0x0050133f`, so every guest dismissed from a queue plays kids `0x80`, not one in eight. Stop the original afterwards |
| `original/inside.py PID SECS [LEVEL [BELOW]]` | The original's memory: the guests inside the park as the analyser counts them (`FUN_004c9130`: a guest on a cell of type 0, 1, 3, 9 or 10) and those of them whose toilet need is over LEVEL (60 unless given), with their ids, the share in whole per cent the advisor's row 82 sets against 25, and the door, a line a change with `mGameTick`. Says who the advisor's and the gate's counts hold on a tick. A `POLL` for `loadfile.sh` (Q347). `inside.py PID SECS LEVEL BELOW` also counts those under BELOW happiness (`+0x19c`), their share and the crowd, the advisor's row 14 (Q415) |
| `original/worldwords.py PID SECS OFFSET[:FMT[:NAME]] ...` | The original's memory: any words of the world (`[0x0080239c]` + OFFSET), a line a change with `mGameTick`: the arrival vehicle that is current and the three slots' things are `0x1da72a:H:current 0x1da72c:H:size1 0x1da72e:H:size2 0x1da730:H:size3`, and the park's generator, `mRandomSeed`, is `0x1da708:I:seed` (a file's own seed is the first read after its load, Q443). A `POLL` for `loadfile.sh`; `thingwords.py` reads a thing's |
| `original/vehicles.py PID SECS` | The original's memory: the arrival vehicles and the leavers, polled every 50 ms: a line a change of the current vehicle word, the three slots' things, each made one's script `VAR_TRIGGER` and `VAR_STATUS`, the load still to drop and the offloading byte, with `mGameTick`; a line each guest made or gone (last state and cell); a census a minute (guests, leavers in `0x12`..`0x15`); and at the end each vehicle's visits, its status becoming 2 (the arrivals' stop) or 4 (the leavers'), with the ticks. A `POLL` for `loadfile.sh`. This game's side is the log's `the <vehicle>'s status A -> B on mGameTick N` (Q627, `q627/ours.py`, `q627/orig/stock.sh`) |
| `original/leavers.py PID SECS [EVERY]` | The original's memory: each guest setting off home (state 6 to `0x12`) with its cause (`day`: the exit level nought at the turn; `miserable`; `shut`; or the state it came from), loaded or arrived (a load is mGameTick going back), each made and gone, every way out of state 6 by where it went (`0x8/anim4` the chooser's failure), and a **happiness ledger**: every guest's change between 50 ms polls summed by the state it came from and went to. A census every EVERY sweeps (242): guests, leavers, deciders, the mean happiness, under 25, exit levels spent, each entertainer's cell, state and watchers (deciding on his nine cells). A `POLL` for `loadfile.sh`. This game's side: `lib/peepcensus.py` and the console's `happiness on` / `happiness` (`Peep.HappinessLedger`, by the state at each write) (Q668, `q668/ours.py`, `q668/orig/run.sh`) |
| `lib/peepcensus.py` | This game's guests from the console's `peeps` (and `staff`) replies, in `leavers.py`'s words: `parse`, `census`, `watchers` (each entertainer's deciding guests round him) and `leavers` (the log's `Person N: leaving, <why>` lines, loaded or arrived). Imported by a script exec'ing `parkrun.py`, or `peepcensus.py run.log` (Q668) |
| `original/entcell.py PID` | The original's memory: the first entertainer's world point (cell x 10 + 5) and state, as `X,Z STATE` for `setcam.py`, so a frame can be taken at him (Q668, `q668/orig/watch.sh`) |
| `original/litter.py PID SECS` | The original's memory: its litter share as the advisor's rows 8 and 9 take it (`FUN_004ccdf0`: every map cell, `+0x8` the type, `+0x2c` `mLitter`), with the littered cells, a line a change with `mGameTick`. A `POLL` for `loadfile.sh`; it shows the original's handyman sweeping where this game's does not (Q417) |
| `original/texcache.py PID SECS [NAME=+MASK\|-MASK\|=VALUE\|@ADDR=...]` | The original's texture cache (`[0x0087a268]`): every texture with its folder, refs and each record's flags word (`0x100` cut, `0x200` graded, `0x300` additive), `LIST=substr` to narrow; given NAME (`m_grass*` a prefix) or a raw record `@0x...`, holds the `0x300` bits (`=`) or ORs/clears a mask for SECS and restores them, `DONE=FILE` touched once the writes stand. A texture whose flags no draw reads (Q361: a procedural base's floor) changes nothing |
| `original/floormats.py PID NAME...` | The original's memory: every model material whose texture is a named cache entry (its source record, found by walking texture -> slot -> material object -> record), its flag word (`0x800` drawn opaque), counted by texture. Keep only words with bit `0x1` and nothing above `0x8f3`: the rest are other structures (Q361: 174 of 174 floor materials with `0x800`) |
| `original/floormesh.py PID` | The original's memory: every loaded model instance with its procedural base as the loader left it (`[model + 8] + 0xc` the base vertices `FUN_00469a80` counted, `+0x2c` the floor mesh it picked, by index and name) and its root mesh's material words (`0x800` = opaque). The original loads every catalogue item on offer, so one park checks them all; ignore the instances counting 0 beside one that counts (other references to the same header) (Q362) |
| `original/squares.py PID SECS [N]` | The original's marker squares, read-only: the fullest face list (`0x00820b00`, count `[0x00871ee0]`) seen in SECS, a record a line (cell, height, texture: 0 blue, 1 red, 8 `m_link`, 11 `m_end`; face `+0x0c`: 0 the square, 1/4/`0x10`/`0x40` a wall on its N/E/S/W edge rising 10 from the height, Q436; orientation `+0x10`), then each texture's cells and those laid more than once. The draw empties the list every frame, so it samples; textures 15 and up are other meshes' polygon faces after the squares. A tool's strip, a stranded person's red square, the locator's patrol edges (Q426), the walls under a lifted square (Q436) |
| `original/dynfaces.py PID MESH [out.json] [N]` | The original's dynamic-faces meshes, read-only: every record of the face list (`0x00820b00`, `0xa0` bytes) whose mesh id (`+0x90`) is MESH, read at once - 1 the sea's 134, 3 the gates' flags' 384 - with their count, words and box, and into `out.json` each one's four corners, UVs and normals (the original's axes, y up). The count is nought between frames; the records stay. Q491's flags against `q491/flagmodel.py` |
| `original/modelnodes.py PID [HEADER\|=MODEL] [NAME...] [--scale F --secs S]` | A loaded model's meshes in the original, read from its node tree (default the park's `Terrain\base.md2`, `@0x007a0854`; `=MODEL` every placed instance whose model is named so, from the slot list `0x007a4614`, Q658): each mesh's name, flag word (bit `0x10` a hidden node: what a clip's hide list put away, Q658), normal count, its normals' four commonest lengths (each NaN is a bin of its own, so a mesh with several shows them one by one, and the rest are not shown) and the mean of the colours its last lighting wrote (`[mesh+0x9c]`); `NAME...` keeps the meshes whose names start so. `--scale F --secs S` WRITES the named meshes' normals times F for S seconds, reads them again 1 s in, then puts every byte back (exit 2 if one was not). Which terrain meshes stand on half-lit land (0.225 long) and what light a mesh was given (Q619) |
| `original/pointsat.py PID HEX...` | Every dword of the original's writable memory pointing at the given addresses (`OFFS=N`: or up to N bytes before them), ~10 s: run it on its own output to walk a pointer chain up |
| `original/footprints.py PID [SECS]` / `PID items` | The original's memory: every placed model instance's footprint table as `ProceduralBase_SkirtFlags` built it (`[instance + 8] + 0x18`, `{ flags, frame }` a cell, the item's `.hmp` giving width and depth), byte 0's `0x40`, angle, covered rectangle and the heightfield words of its cells (frame << 16 \| word); `items` prints every loaded item model's table instead (the list at `0x007ae6a8`), placed or not, so one park reads them all. Run after `loadfile.sh load` (Q649, `q649/orig/run.sh`, `run-items.sh`) |
| `original/brk.sh ADDR[,ADDR...] [HITS] [SECS] [WORDS]` | Who CALLS a routine of the original: a hardware breakpoint through `gdb` (up to four addresses joined by commas, each hit naming its own; Q364), attached and detached again; at each hit `mGameTick`, the registers and the stack's words that point into the game's code, nearest first (the first is the caller's return address; read each in Ghidra, a real one follows a `CALL`). `watch.sh` says who writes an address, this says why. `BRK_PRINT='printf "fctrl %x st0 %.17g st1 %.17g\n", $fctrl, (double)$st0, (double)$st1'` adds one gdb line a hit: the x87's control word and stack at a compare. `BRK_COND='*(int*)($esp+12) == 0xd0'` makes every address a conditional breakpoint, so a routine called many times a second is caught only for the call wanted: at a routine's first byte `$esp+4` is its first argument, so this is `Sound_PlayEffect`'s effect, and with `0x0051bfc0,0x0051c130` it names who plays effect 208 (Q567, `q567/orig/run.sh`) |
| `original/flagangle.py PID SECS STEP` | The original's memory, read-only: the angle the gates' flags stand at, read every STEP s for SECS from the first flags face of the face list (`dynfaces.py`'s read): its corner [0][0], 0.5 off the pole (411.25, 22, 38.75), `atan2( dz, dx )` in degrees, a stamped line a read. With `thingpoke.py %PID% S 15 7:0x5c:f=W` it shows a change of wind land in one frame (Q643, `q643/orig/run.sh`) |
| `original/thingpoke.py PID SECS KIND THING:OFFSET:FMT=VALUE ...` | The original's memory, **written**: holds words of things at given values, written again whenever they read otherwise, a line a write; refuses a thing whose type byte (`+2`) is not KIND. `150 15 7:0x38:B=1` holds the weather's `mLightning`, so bolts and thunder come on a clear day. `thingwords.py` reads the same words. Stop the original afterwards |
| `original/mixpairs.py [<mixer.py log>]` | A `mixer.py` log against the category files: every pair of a 3D channel's far end and volume of 127, with the variation volumes of that range that `round( v x 0.75 )` makes it from. Says which effects' channels a log holds, and that none plays at a number the files cannot make |
| `original/modelboxes.py PID [FIRST LAST]` | The original's memory: each thing's model record's cell rectangle (`+0xc0`, `+0xc4`), whether it has a `.hmp`, the box's middle (where its scream is sounded) and its base height. Read the objects' lines (the stock park's are things 13 to 24): a person's `+0x20` is no model handle, and its line is noise |
| `original/labread.py PID [SECS]` | The original's research lab, live: one dump (its projects with their points, the five kinds' efforts, outputs, current projects and highest groups, `mWorkLoad`, `mLabActive`), then with SECS a poll at 20 a second printing each project FINISHED with its `mGameTick` and points, each kind's current project or highest group changed, and every 10 s each kind's current points. The lab is `[0x007cfb90 + 20 * world[+0x1da722]]`. Run as `loadfile.sh`'s `POLL`, and again with a longer SECS after the load: the original's `mGameTick` moves about 6 a second off-screen (Q701) |
| `original/respump.py PID [SECS] [POLL_MS]` | The original's research screen pumps, live: the tree `[0x007cc1d8]` walked for the five panels (`0xd2d01`, `0xd2d07` to `0xd2d0a`), each one's slots from `+0xa0` (position 16.16, the pass's clock stamp, speed, state), one block; with SECS a poll every POLL_MS (2) and at the end, per pump that moved, its passes (a new stamp each), passes and frames a second over the wall clock, the stamps' step and `mGameTick` at both ends. On the real screen at the 30 fps cap: 30.00 passes a second (Q705) |
| `original/progress.py PID [SECS]` | The original's player progress, live (`[0x00f7c58c]`): the current park's record (`+0x48`) and every park record in its tree by theme name, each with renamed (`+0xb8`) and all-researched (`+0xb9`), a line a change with `mGameTick`; with each record its six tickets and four records (`+0`, `+6`, `+0xc`), and the player's own record and secret tickets, spent, keys given and the ticket-bought ride ids (Q591; the set, Q709). A `POLL` for `loadfile.sh` and `fsload.sh`; the in-memory twin of `gmsfile.py`, before anything writes `gms.dat`. Q550 |
| `original/ticketmeasures.py PID SECS` | The original's memory: what golden tickets Local 0 to 2 read, a line on each `mGameTick` that is a multiple of 100 (the test's own ticks) and on each change of the current park's six tickets: the cumulative visitors (analyser `+0x21c08`), the guests inside as `FUN_004c9130` counts them, their mean happiness as `FUN_004c7bb0` takes it (each `+0x19c` cut to a whole number's low byte; 0.0 with none or the park shut), `mParkClosed`, and the six tickets (`progress.py`'s current record). Lines up with this game's `Golden tickets: tick N, visitors V inside I happiness H` log line. A `POLL`, or started by hand on `original.sh pid` after a load (Q629) |
| `original/camread.py PID [SECS] [STEP]` | The original's park camera and interface, live, a line a change: `gui_CameraFlags`, the ride view's model, the look offsets, the eye, layer 0's and layer 1's visibility bit, the music dip word (`[0x00803ad0]`; `[0x00803ac8]` is only the volumes' dirty flag), the End feature's id and the compositor word, with the state machine's step counter `[0x00877d34]` (`step`; not `mGameTick`, which `tick.py` reads). A `POLL` for `loadfile.sh` (Q551: the End's ride view, `q551/orig/run.sh`; `park-engine.md`, "The End feature's view") |
| `original/research.py <in> <out> <item> [0]` | A copy of a jungle park file with one item marked researched, so Instant Action's buy list offers it; with a last `0`, marked not researched, which both games keep for an item whose research costs nothing (Q335) |
| `original/savepark.sh <NAME> <dest.TPWS>` | The RUNNING original (off-screen) saves its park from its own menu: Escape, Save, NAME typed, the tick; the file is moved out of the reference player's jungle folder to `dest` (frames `dest-typed.png`, `-after.png`), so the install's `save/` sum stays (`gms.dat`'s time changes). A file of the original's own to set beside this game's (Q523). `PLAYER=2fsfresh` saves from `freshfs.sh`'s player, and so writes its `gms.dat` (Q709) |
| `original/advdraw.py PID [SECS] [STEP]` | Is the original drawing its advisor, live, a line a change: `gui_CameraFlags`, `g_Advisor_Busy`, the speaking slot, the one view's flags, and for slots 0 and 1 the node word, whether the model's renderable is in render list 1 and whether its draw stamped it in the last two frames (`drawn`, `stale`, `never`), each line ending with the wall clock to time a `record.sh` film by. A `POLL` for `loadfile.sh` (Q556; `ui.md`, "How he reaches the screen") |
| `original/advnodes.py PID SECS [SLOT]` | The original's advisor model as drawn, live, a line a change: the renderable's two screen-space matrices (A at `+0x10`, B at `+0x50`) and the draw flags, then each node of the tree depth first, its basis lengths (a scale track's work), translation, column 3 and the w column of its chain to the screen, with the flags of the clip word handed on most recently (it plays after the running clip; one clip ahead of what is drawn, give or take a poll) and the tick. A `POLL` for `fsload.sh` (Q597; `ui.md`, "His projection is affine") |
| `original/advlight.py PID [SLOT] [--secs S] [--json OUT]` | How the original lit its advisor, live, read-only: the ambient the model lighter starts from, the advisor record's own light (type, colour, direction), and for each mesh of slot SLOT's model (0 the global fly, 1 the theme's) its lit colours from the cache `[+0x9c]` (min, max, mean, red = green, blue less red) and the light vector fitted in the mesh's own normal space (`|v|` its strength, the channels the law then misses). In a park: ambient (0.333, 0.333, 0.408), `|v|` 2.00, 0 off (Q621; `ui.md`, "How he is lit"); in the lobby (0.267, 0.267, 0.267), `|v|` 1.00, slot 1 empty (Q657: the original started fresh greets on the player-slot screen within seconds, so poll it every few seconds from the start, `q657/orig/run.sh`). Run it while a line plays, after `sayas.sh` for a costume |
| `original/coaster.py PID [SECS] [full]` | The original's coasters, live (`[0x00790fe0]`, next `+0x18`): each one's instance, handle, highest track point `+0x110` (Global 0's measure), top speed, type factor, sample count and spacing with the largest sample y and where, then every node of its chain (cell, ground place, pylon heights, stack, loft, place, yaw, bank, incline, its knots); `full` adds each knot's handles and direction and every sample. A block whenever the summary changes; a `POLL` for `fsload.sh` / `loadfile.sh` (Q593; `q706b/curvecheck.py` rebuilds the samples from a `full` dump) |
| `original/trackride.py PID SECS [BT]` | The original's track rides (karts, water rides, bumper rides), live, a block a change: each record of `[0x00877b60]` (BumperType BT alone if given) with the object holding it and that object's `mIsTrackRideValid`, duration, state, cars, seated, its motion record at its performance, centre, unload point, turn flags and race buoy; every buoy (flags, place, object) and section (type, cell, collision objects with their flags and radii); each live car (flags, place, velocity, buoy, timer, bonus, patience, heading, `+0x58`, turn, mesh, clip); and the build tool's mode and anchor (Q581) |
| `original/original.sh`, `tpwmem.py`, `gmove.py`, `record.sh`, `watch.sh` | Starting, reading, clicking, filming and watchpointing the original ("The original under Proton", below) |
| `original/timens.py SECONDS CMD...` | A command run in a time namespace where the computer has been up SECONDS: the original's park clock without a restart (warning 1, below). `original.sh start` runs the game in it, 60 s, by default (`ORIG_TIMENS=0` starts it plainly). Ubuntu's `kernel.apparmor_restrict_unprivileged_userns=1` refuses it to a plain process (`PermissionError` on `/proc/self/setgroups`), so it runs under Steam's AppArmor profile, which allows user namespaces: `aa-exec -p steam -- python3 timens.py 60 CMD` |
| `original/kwinwl-start.sh` | A private display on `:78` drawn by the graphics card (`kwin_wayland --virtual` with Xwayland) for the original, where Xvfb draws by software. `:78` is `parkrun.py`'s too. **Not run since it was written**: try it before leaning on it |
| `q119/lib.py` | What `parkrun.py` is built on: the launch, the console pipe, XTEST clicks and keys, the frame grab. Use it through `parkrun.py` |
| `fork-review-2/forkscan.py [--keep forkscan-<date>.json]` | Every fork in OpenTPW's network, three levels down (ours left out), through the logged-in `gh`: which forks are new and which branches moved since `forkscan-state.json`, each moved branch compared on GitHub with upstream's main and ours (a copy of a tip either holds is skipped; a fork of over 30 branches has only its default branch compared). Prints `WATCH` for a branch ahead of both, writes `forkscan-<date>.json`; `--keep forkscan-<date>.json` records that scan's tips once its moved branches are handed on, and scans nothing (a fork may have moved since). About 45 s |
| `fork-review-2/forkwatch.py [--fork OWNER/REPO[@BRANCH]] [--from SHA] [--keep watch-<sha>.json]` | The daily fork watch's first step (read-only): with no `--fork`, fetches the meneerkrabs fork's clone (`~/.cache/tpw-fork-krabs/repo`); with one, any fork `forkscan.py` names, cloned to `~/.cache/tpw-forks/OWNER__REPO`, its state `watch-state-<slug>.json`, its first look from the newest merge base with upstream's main or ours, its leads `watch-<slug>-<sha>.json`; takes the PC addresses in the lines added since the last look, maps each to its function by `funcs-all.tsv` (every function's ranges, dumped from Ghidra 2026-10-16), drops the functions our tree and the FileFormats clone cite now and those an earlier look handed on, and writes `watch-<sha>.json`; `--keep watch-<sha>.json` records that look's tip in the state once its leads go to a sweep, and fetches nothing |
| `fork-review-2/mkwatchev.py <watch-SHA.json> <dir>` | The watch's evidence builder: splits the leads into areas (advisor, ui-hud, audio-fx, world, other) by the fork file citing each most, and writes `<dir>/claims-<area>-N.txt` (the fork's citing lines, +-2, read from the watched tip, for the sweeps) and `<dir>/code-<area>-N.txt` (our Ghidra decompile of each function, for the verifiers) from `<dir>/decomp.json`, which a Ghidra dump script (`watch/2026-10-16/dump.py`, a model for the next) writes |
| `gen_addresses.py` | Regenerating `docs/exe/addresses.md` |
| `lib/cliptracks/` (a project: `dotnet run --project ~/.cache/tpw-harnesses/lib/cliptracks -c Release -- <data dir> [clip]`) | Every shipped clip's position tracks against its model (Q554): the counts (late first keys, last keys before the declared span, additive models, nodes under a moved node, a route in the same clip) and the lists `late.txt`, `past.txt`, `offrest.txt`, `nested.txt`, `adds.txt` in the working folder; with a clip's path, its declared span and each position track's keys and samples. Builds the repo's `OpenTPW.Files` in Release |
| `rse-listing/bin/Debug/net10.0/rselisting <file.RSE>` (`DOTNET_ROOT` set) | One ride script's listing: its variables by index, the variable it watches, and each instruction at its word with its operands, variables by name and branches as `->word`. The `.RSE` corpus (`CLAUDE.local.md`) is what to point it at; `lib/soundsweep.py` reads its output. A turn of a script with no park can be counted from it by hand (Q144: Coaster1's 993 gaps in 200 turns) |
| `wadcat`, `strdump`, `nodenames/` | A wad's entries, a string table's lines, a UI wad's node names (`CLAUDE.local.md`). `strdump` prints every part of a row, a parameter as `{Pn}` (since Q372: a row that starts with one, as UITEXT 441 "{P11} hours", read blank before); name the table under the data folder, `Language/English/UITEXT.str` (it reads through the tree it is built on, which decodes each language folder through its own `MBToUni.dat` from Q710) |
| console `strings <table> <row>...` | A string table's rows in the running game, through the table's own folder's character table (Q710): `strings Language/american/THEMENAMES.str 0` answers `strings: Language/american/THEMENAMES.str row 0 of 4: "Lost Kingdom"`. The log's `Character table: <path>, <n> characters` says each character table opened, once each |

**A poller of the original's memory is the one thing still written an item at a time** (`q257r/orig/cars.py`,
`q257x/orig/smoke.py` for a car's smoke handle, its emitter and its node's place, `q257t/orig/chan.py` for a car's model's channel stamps, `q253/orig/look.py`; a model's heads are `original/heads.py`, above): each reads its own structures. Start from the newest that reads the same
structure, and hand it to `loadfile.sh` through `POLL`.

## Cameras in both games

Read this before aiming a click or comparing two pictures. Each fact names the item that found it.

| To do this | Use |
|---|---|
| Set this game's park view | console `camera X Y ZOOM YAW` |
| Read the original's view | `original/camera.py PID` (`--aim X,Y` gives a cell's pixel) |
| Read the original's lens | `original/lens.py PID` |
| Set the original's view | `original/setcam.py PID X,Z ZOOM ROT off` |
| Put both games on one camera | `lib/bothcam.py` (`both()`, or `X,Z ZOOM ROT PID`) |
| Stop or restart the original's follow | `original/setcam.py PID off` / `on` |
| Pan the original to a cell | `original/camto.py PID X,Y` (pointer on the screen edge; it stops a follow) |
| Watch the original's pan and its edge | `original/campoi.py PID 7 0.03` beside a held keypad key |
| Watch the original enter and leave first person | `original/campoi.py PID 22 0.03` across the pick, its click and C (`q626/orig/run.sh`) |
| Click a named cell in the original | `original/cellfind.py PID X,Y`, straight before the click |
| Click a named cell here | `lib/cellprobe.py` `find( (x, y), start )` |
| Find cells outside the park in the original | `original/outside.py PID` |

**This game** (`ParkOrbitCameraMode`):
- `camera x y zoom yaw` takes world units, a cell times ten, and the yaw in degrees; its reply is the state (`poi=... yaw= zoom= pitch= ground=`). Q257n
- Both games at one camera: copy the original's point, zoom and rotation from `camera.py` as `camera X Z ZOOM -DEGREES` (the original's third coordinate is this game's second, and its rotation is this game's yaw negated: at 90 the same eye is -90); `lib/bothcam.py` does it. Q435, Q440
- The eye is lifted by the ground's height under the point, so over high ground it is further than `zoom` from it. Read `placed`'s listener, do not work it out. Q264
- The camera aims at the ground: it cannot close in on water 30 units up. Q257s
- A person's drawn size in both games: the mechanics' locator (Ctrl+S, then the button `95f`, the original's at (360, 613)) puts Mike Cooper's place at the frame's middle at zoom 110 in both (w 77.782 there); read his picture by `circle 26` here and `spritepick.py PID where 26` there, and mark the frame with `lib/cropgrid.py`. Since Q500 his whole picture is drawn here with the locator open or folded, as there; `circle` ends with the depth he is tested at ("tested 106.19" at 110, his depth itself in first person). Q470 (`q470/confirm.py`, `q470/orig/run.sh`)
- **A person's own pixels and sides in both games** (Q475): here, under `pause`, a frame, then `sprites 0` (the people's pictures, balloons and bubbles not drawn, the pick's record kept), a frame, `sprites 1`: two paused frames are the same to the bit off the people, so every pixel differing by more than 2 is theirs; `circle THING` gives the origin's own pixel (`origin pixel`) and the picture's size, origin and reflection, and the sides are the origin's pixel minus and plus (origin x, width − origin x) × 60 / w × the frame's height / 768 (here w is `circle`'s depth × sin 45), swapped when reflected (`q475/confirm.py`, a `[W H]` for `size`). The original cannot hide its people: freeze the frame and its memory as one with `kill -STOP` on the game's pid, read `spritepick.py PID where THING` before and after the `import` grab, then `kill -CONT` (`q475/orig/run.sh`: before and after equal at 10 of 10), and read the sides by eye on a `cropgrid.py` crop with both rules' marks. **Never let the command that starts an off-screen run hold the words of `original.sh`'s display test** (`Xvfb` and the display number, in a note or a check in the same shell line): its `pgrep -f` finds that shell, the private display is never started, and KWin, `gmove.py` and `xdotool` then fail with "Can't open display: (null)". Q475
- **What a constant of the original's draw does, seen in one pose** (Q477): hold the clock with `memhold.py PID 14 0x007859a0:I=1` (the people stop, the frame keeps drawing; this sets only the clock's paused flag, not a real pause: while held the clock reads the capture of the last real pause, which can be earlier than now, so it may step back and then stand, and when the flag is put back the held span falls due at once, so take nothing timed just after it), grab a frame, hold the constant at another value with a second `memhold.py` for a few seconds and grab again, then once it is put back; the first and last agree off the corner the pause leaves moving, so the middle frame's difference is the constant's alone. Mike's depth pull `0x00702044` held at 0, -0.02, -0.05 and -0.2 under the mechanics' follow: `q477/orig/run.sh` (`KS="K K K"`, a cycle a value) and `q477/orig/trio.py` (the three crops with the origin's row marked, and the rows that differ). Our side's lowest drawn row of a person against the origin's row: `q477/ctl.py`. Q477
- **Who in the view is hidden by what is nearer** (Q500): under `pause`, `circle` with no thing lists every drawn person (window pixels, depth, picture, "tested" depth), and a frame with and one without people (`sprites 0`) give each one's own pixels; set against their picture's rectangle, the fewest are the ones something nearer covers. `q500/more.py` (`cut()`: the census, the eight most covered, a crop sheet of four; at the start view the park gate's stone hides two), with first person's census (`camcorder`) after. Q500
- `freeze` stops only the lobby's orbit. `pause` stops the clock, and nothing eased on `Time.Delta` moves under it: `step` before a grab (`VERIFYING.md` rule 91). `camera` then `step 2` moves every walker two ticks, so take a census last. Q257r
- `cellprobe.py` needs the camera set first (`camera X Y 60 0`): at zoom 25 a 50 to 90 px step stays inside one cell and the slope is nought. Anchor nothing before both corners are found. Q433

**The original** (1024 x 768 off-screen frame at its origin):
- `camera.py` reads the point `0x007908f0` (x, ground, z; a cell is 10), zoom `0x007909ec`, rotation `0x00790a38` and the drawn rotation `0x007909e0` (eased after it), the camera matrix `0x0087aff0` (its last row the eye) and the draw matrix `[0x0087b088]`. Q435
- Its eye is the formula's: point + (-h sin r, sin p x zoom, -h cos r), pitch from the zoom (`park-engine.md`, "The park camera"), measured at rotations 0, 90 and 180. Q435, Q440
- Its arrow keys turn the view and do not pan. The pointer past 0.95 of the way to a screen edge pans (only with `[0x0078d910]` clear, an Options toggle), about 8 cells a second, and stops a follow: `camto.py` learns the sense and rate, then rests the pointer in the middle. A pointer left there by a script keeps panning (Q440's first round lost two rotations to it). Q433, Q439, Q440
- `setcam.py` writes the camera words (with `off`, or a follow puts the point back the next frame) and reads the eye back: 0.000 units off the formula at every rotation tried. Q440
- **Its zoom stands between 70 and 130**: its update clamps it, so `setcam.py` given 150 reads back 130 while this game's `camera` takes 150, and the two pictures differ by 150 / 130 (pixels mapped from one land elsewhere in the other: Q478's first round met the Litter Bin for the Drinks Shop). Keep a shared camera's zoom inside 70..130. Q478
- A file entered by `screenpark.sh` is framed on the camera's own point; the Load Park list's view is 65 units on. `parkrun.original_view( x, y )` allows for the list. Q359
- For the first seconds after a load the camera is not on the saved view: poll a short-lived thing, do not film it. Q257w
- A camera file's listener is not where the camera was aimed: read `voices.py`'s first line. Q263

**The follow**:
- It is `gui_CameraFlags` `[0x00790ab0]` bit `0x1000` (`park-engine.md`, "The follow"). The original starts it when the locator's button finds somebody, on Ctrl+S or Ctrl+V with a person's window up (with nobody it opens a locator and leaves the bit clear), and on a window's binoculars, which put the window away; the point then trails the member as he walks, the arrow keys only turning the view. Q435, Q440
- A person's window, from the strip or the world, folds the locator and stops it: with a window open the camera stands still (Mike walked 12 units in 2 s, the point did not move). Q440
- It ends on any pan (the keypad, the pointer past 0.95 of the way to an edge: a second at the right edge left the point 83 units from Mike), the locator's fold, a mode change, and a person's window opening (the strip's click folds the locator, so the camera stands still while the window is up; a frame from before that click is not the view at a PICK UP). `setcam.py PID off` ends it from outside, `on` starts it again. Q432, Q435, Q440
- This game: `ParkLocator.Press` sets `Following`; each frame `Camera` puts the point on the member's blended place. A change to the camcorder's first person, a person gone, or a step that finds nobody clears it. A staff window's preview click does `GoToThing` once. Whether opening a window from the world click follows here is not checked.
- A walker's place against the camera is read under `pause`: two replies apart he has walked most of a cell. Q424

**The lens** (Q260):
- Every view hands the projection an angle (the park 90, the lobby `ISLANDFOV`), and the draw matrix's fixed screen scale makes the picture `2 atan( tan(a/2) / 2 )` high: the park 53.13 x 67.38, the camcorder the same, the lobby at 100 61.58 x 76.93 (`park-engine.md`, "The lens"). `original/lens.py PID [SECS]` reads it live; this game logs it once a mode (`Camera: ParkOrbitCameraMode lens 53.13 high at 4:3, ...`). Q260, Q441
- This game draws the same lens (Q441): its 1280 x 720 pixel is (640, 360) + (the original's - (512, 384)) x 720 / 768 at the same camera, six named cells on the first probe at three rotations (`q441/confirm.py`), and its middle 960 x 720 scaled to 1024 x 768 lays on the original's whole frame (`q441/sidebyside.py`, `q441/side-by-side.png`). The scripts of Q260 to Q440 (`q260/confirm.py`, `q435/confirm.py`, `q436/frames.py`, `q440/confirm.py`) were written for the old 90 degree lens and use x 360 / 768 and the middle 480 x 360: change both before running one again. Q260, Q435, Q441
- A change drawn at one camera is shown by the box difference against an earlier run's frame at the same numbers. Q436
- One first-person stand in both games (Q485): the original's way in (Q496's, below) stands its eye at (481.897, 5, 166.285) looking (0.2788, 0, -0.9604), and `camera.py PID` there prints the first-person draw matrix and eye (`q485/orig/b-camera.txt`); here `pause`, the pointer at the window's middle (Steer turns the view off it), then `camcorder 481.897 166.285 -2.85905` (the yaw is the original's rotation negated, as `bothcam`'s). A ground point put through that draw matrix lands on ours by the same 720 / 768 map: `q485/linefit.py`. Q485
- The original's records do not depend on its camera, so a run whose camera went elsewhere still gives them. Q436
- The original was once read as standing "further back" (Q257r, repeated by Q264) and once "closer" (Q357) at the same numbers. "Further back" was the Load Park list's view, 65 units on (Q359); "closer" was the lens: the eye is the same (Q435) and the picture showed the middle half each way against OpenTPW's old 90 degree lens, so everything stood twice as large in it (until Q441). Q260
- `camera.py --aim`'s pixel was cellfind.py's first probe at both of Q260's cells, with the camera still: the cells out at Q439 came from something other than the lens. Q260

**Aiming a click** (every one of these cost a void):
- Set the camera first (`setcam.py ... off`, or `bothcam.py`), then aim with `camera.py --aim` and click through `cellfind.py` (`PICK=1` with a window open): six of six on the first probe, and a click on a named cell with Mike's window open landed on it (the picker 4018 = (49,31) at the press). Q440
- Read the camera in the same run as the click, straight before it. A fixed pixel or cell from an earlier run named other cells (Q435 O1, Q438 O3).
- Aim relative to the camera's own point, not to fixed cells (`q438/orig/run.sh` `AIMREL=1`); better, take any cell of the right kind found live (`outside.py`, `cellfind.py`) and never hard-code Q433's (51,27), (54,26), (45,15).
- A cell off the frame stops the run unclicked. Q439 stopped at 877,38 and at 2003,686.
- A frame and its camera read seconds apart are not the same view while a follow runs. Q438
- Stop the original on any exit of the run script, or the next run loads into it. Q439
- The original's picker reads straight off the pointer: `camera.py --aim`'s pixel, one `xdotool mousemove` on `:77`, and 1.5 s later the cell `[0x007b05cc]` and its hit point `[0x007a1c70]` (three floats, x ground z), which stand unchanged where the ray meets nothing (`q472/orig/run.sh`). Q472
- The pixel map to this game holds only where both points stand on the same ground: at (400,780) this game's camera stood on ground 7.0 and the original's point slid to (400, 781.6) after `setcam.py`, and the mapped pixel fell two rows short. There, find this game's pixel by `pick X Y` down a column (`q472/confirm.py`, G6b). Q472

## Recipes

Each was found by trial in the item named, and cost a run to find.

- **Which pixels a change draws, and the same box in the original** (Q649): the frame before the change and after it at the same paused camera (an earlier item's run at the same numbers serves, `q618/runA/B-1.png`); every pixel differing by more than 30 in the sum of its channels is the change's (people and animated models aside), so a `lib/sharp.py` box wholly inside it is on what changed and one with no change is not (Q649's first two boxes, mapped by eye, were not). A box of ours maps to the original's 1024 x 768 frame by `bothcam.py`'s rule: x' = (x - 640) x 768 / 720 + 512, y' = (y - 360) x 768 / 720 + 384 (`q649/PREDICTION.txt`, P4c).
- **A gate: a run paused on a tick, to predict from its census before a turn** (Q713, `q713/confirm.py`). A resume
  then `pause` overshoots by eleven to twenty-one sweeps (the tick is read through the console every 0.4 s, and a line
  playing slows the replies), so a stop meant before a poll turn lands after it. Resume only to fifteen ticks short,
  `pause`, then `step 9` until the tick is reached (off-screen nine frames are about a sweep): the stop lands on its tick.
  At the gate print the census, then wait for a file the session writes after its prediction (`go-<tick>`). Under such
  pauses and steps GameClock's milliseconds and the sweeps drift apart (a line's 7.5 s wait took 88 sweeps), so a
  prediction that a line waiting behind another is said "by tick N" is not to be made from the original's four sweeps
  a second.

- **One frame of this game held on screen, the frame a real click is dealt in** (Q623, `q623/run4.py`): `pause` and
  `step` cannot show it, since the next frame's sweep runs before any grab, and a full-rate film misses one frame.
  `stall N` sleeps at the END of `Level.Update`, after the clicks and before the draw, so chain three: send `stall
  1500`, click by XTEST and send `stall 1500` within it (the next frame reads both, deals the click and stalls before
  drawing it), then send a third `stall 1500` during the second: the frame after reads it and stalls before ITS draw,
  so the click's own frame stands on screen for 1.5 s. Grab in the third stall's window, timed by the replies' `stall:
  N ms from wall W` lines; a grab one window earlier is the frame before the click, the control. The park clock then
  catches up the 4.5 s at once, so frames after it are not the next ones. In the original the same placement filmed
  by `record.sh` (`q623/orig/`): `setcam.py` after the buy screen's row (the align sweep pans the camera), Buy
  (117,588), the features tab (843,123), the Round Fountain's row (570,261), then the click on the ground.

- **The game's `console.log` and a harness run** (Q647). Every launch writes `console.log` and keeps the launch before as `console-prev.log`, in the game folder beside `save/` (the player's own data folder where that cannot be written; `OpenTPW.Common/ConsoleLog.cs`). A harness or test run that starts the game sets `OPENTPW_LOG_DIR` to its own run folder, or the run rotates Alexah's pair away: `parkrun.py` does it by default (`OUT`); a hand launch needs `OPENTPW_LOG_DIR=<folder>` beside `OPENTPW_GAME_PATH`. The log holds what stdout and stderr printed, so it equals the run's `run.log` with a header line (the build's commit, the start time) on top. The new file is opened first, as `console.log.new`, and `console.log` is moved over `console-prev.log` only once it is open, so a folder that cannot be written keeps both older files (a move that fails after the open leaves the run's file named `console.log.new`, said on the terminal). A launch that finds no game writes no file.

- **The daily fork watch** (2026-10-16, `fork-review-2/watch/2026-10-16/`): what the meneerkrabs fork newly reads from
  the PC executable since the last look, and what upstream (github.com/OpenTPW/OpenTPW, every branch, from 2026-10-19)
  newly does, handed to Alexah as a report. (0) `upstreamwatch.py` (read-only clone `~/.cache/tpw-fork-upstream`, tips per
  branch in `upstream-state.json`, `--keep upstream-watch-<date>.json` once handed on) says which upstream branches moved; a moved branch's readers
  get a sweep and a verify as below from `upstream-lobby/sweep-brief.md`, and a move of upstream/main is the merge task,
  reported, never merged from the watch. (0b) `forkscan.py` (Alexah, 2026-10-25: every fork of OpenTPW, not one) names
  the other forks' new and moved branches; each `WATCH` branch gets `forkwatch.py --fork OWNER/REPO@BRANCH` for its
  executable leads (dumped and swept with the rest) and a sweep of what its readers and docs know about the data files
  (`watch/2026-10-25-forks/BRIEF.md` is the model), a verify as below, then `--keep` on both. (1) `forkwatch.py` writes `watch-<sha>.json`.
  Each script's `--keep` takes the file of the look handed on and records that look's tips, fetching nothing, so a
  fork that moves after the look is seen by the next one. (2) Make `funcs.json` by hand, a list of the leads' `func` keys (no script writes it;
  `watch/2026-10-16/dump.py` reads it), run a dump script like that one through the private Ghidra
  (`ghidra-private/gd`, never the `ghidra` MCP server while a session holds it) to write `decomp.json`, then
  `mkwatchev.py watch-<sha>.json <dir>`. (3) One sweep agent per area (`tpw-sweep`, Sonnet) reads
  `claims-<area>-N.txt` and writes `sweep-<area>.md` from a `BRIEF.md` (who we are, the fork, its Mac and Theme Park
  Inc traps: copy the last day's). (4) One `tpw-verify` agent per area settles every high and medium lead and every
  conflict from `objdump` of `testme.exe` and at most fifteen private `gd` calls, reading our facts from committed
  `main` by `git grep` (`VERIFY.md` is its brief), and writes `verify-<area>.md`; only CONFIRMED, or the confirmed
  part of PARTLY, is used. (5) Write `REPORT.md` (leads that held and what they serve, our doc lines found wrong) and
  report to Alexah. (6) **File only on Alexah's word** (hers, 2026-10-16, in the coordinator session: "file and fix"):
  notes under the items the leads serve, new items after the queue's last, our wrong doc lines fixed, each citing
  "fork watch <date>" and the verify file's lead id. Never a sweep-only claim.
- **A member's walk watched in the original, and a patrol area set without the tool** (Q434, `q434/orig/run-b.sh`):
  the person's own `+0x8`/`+0xc` are not its place. The navigator is at `+0xd4`, so `thingwords.py ... 26:0xdc:I:px
  26:0xe0:I:py` (16.16: the cell is the value `>> 16`), `26:0x18:H 26:0x1a:H` the destination (cell x 256 + 128 at a
  cell's centre), `26:0x11c:I` the stamp of the last route planned (it moves on every successful roll), `26:0x30:I` the
  thought and `26:0x19c:I` the state. The patrol tool's click writes `+0x20a`/`+0x20c` and nothing else
  (`FUN_00506e50`), so `thingpoke.py %PID% 0.5 4 26:0x20a:H=3380 26:0x20c:H=3511` (kind 4) fences a member exactly,
  where Q433's clicks P1/P2 land on other cells as the camera follows Mike between runs.
- **A build tool's money refusal in both games** (Q542, `q542/orig/run2.sh`, `q542/confirm.py`): a park with a low
  balance is `parkfile.economy_set( stock, dst, dict( balance = 50 ) )`, but in the original the gate's takings lift it
  by about 125 before the first read after `loadfile.sh`, so hold it: the bank is the thing at world `+0x1da726` (8,
  kind 16), `thingpoke.py %PID% 120 16 8:0xc:i=50`, and `bank.py` reads it. The tools' cursor is the short at
  `[0x007cb2d8]` (8 `c_noplace`, 20 `c_cash`), `d5c` the money flag, `[0x0081af4c]` the run's total, read with
  `tpwmem.py watch 0x0081af4c:i 3` (it reads 0 between resets, so take the greatest). Here `load()` pauses, so the
  file's balance stands; `strip X Y` prints `run total N` (the second charges in it) and marks a square refused for money `$`, and `pointer` the
  cursor. **Our Load Park list is in name order** (`SaveFolder.SavedParks`): with two files copied in, row 1 is the first
  by name, not the newest, so take the first out before loading the second.
- **The patrol tool in either game** (Q433, `q433/orig/run.sh` and `run2.sh`, `q433/confirm.py`): on the original's
  1024 x 768 frame the staff window's patrol switch is (739,385); with the tool armed `[0x008186b8]`, `[0x008186b4]` is
  the preview's cell, so moving the pointer over a grid and reading them maps the view (`run2.sh`). The original's
  camera moves with Mike between runs, so read the cells in the same run as the clicks. **Its Left key turned the view
  and Down drew it back; neither panned**: the pointer resting on a screen edge pans, about 8 cells a second;
  `KEYS="Left:5" EDGE="1022 300 1.2;512 766 1.0"` brought the band outside the park (x 42..53, y 14..16) into view.
  Here `camera X Y 60 0` (10 units a cell) and `lib/cellprobe.py` find the cells; the console's `strip` is the outline
  and `pointer` its cursor. **Set `camera` after the switch is down**: the strip's locator follows Mike, so a camera set
  before Ctrl+S is lost; at `camera 475 190 90 0` cellprobe finds (43,16), outside the park, and (43,21), whose rectangle
  from (43,16) is blue, so one anchor shows a red pass then a blue outline (Q534, `q534/confirm.py`). **An anchored one-cell rectangle is four squares** on that cell (each line lays it):
  measured here, read in the original's code. A run that waits longer than a line (0xc1, 18 s) before the click that
  puts the tool away shows no cut. **Here `0xc1` leaves its slot a tick after it is posted**, as the advisor starts
  saying it: read `advisor`'s `message=193/.../said=1`, not `slot=193/` (Q437's first miss); a way out's cut is the
  log's `withdraw message=193 ... cut=1` just before its own line (`q437/confirm.py`). **A quick right click** (Q438): in
  the original `xdotool mousedown 3`, 0.08 s, `mouseup 3` on `:77` (under the 200 ms limit); here `button(3, ...,
  held=0.06)`. Opening Mike's window again snaps the original's camera to wherever he has walked, so aim that phase's
  cells at the camera's own point (`q438/orig/run.sh`, `AIMREL=1`). A right press of any length lands, one let go inside a slow frame too (Q499; `button(3, what, held=0)` is
  the original's one call `xdotool mousedown 3 mouseup 3`); with the hand full by `carry 1100` and `rmbcancel on`, a
  put-away is the log's `world click: right click - drop:` and `hand` reading `item 0` (`q499/confirm.py`, which also
  leaves first person by it; the original's `q499/orig/run.sh` reads first person by `helpbar.py`'s flags). Each
  release logs `right release: armed=<0|1> held=<ms> ms`. **A stopped frame** (Q533): here the console's `stall <ms>`
  stops the frame once the park has had its buttons and tick (`DebugConsole.StallFrame`, the end of `Level.Update`); send
  it 50 ms after an XTEST press and the release 300 ms later (`q533/confirm.py`). In the original, `kill -STOP` the
  game's pid, the release, then `kill -CONT` (`q533/orig/run.sh`): where the stop falls in its frame is not chosen, so
  count rounds (3 of 5 put away). **A refused click in the original** (Q439,
  `q439/orig/run.sh` with `AIMB=1`): `camto.py %PID% 48,18`, then `outside.py` for the nearest band of outside cells and
  `cellfind.py` for each corner, read straight before its click. Its sound under the music scored 0.32 only: switch the music off first (`q439/PREDICTION.txt`). With it off
  (Q442, `q442/orig/run.sh`): `setcam.py PID 435,165 110 0 off` draws (43,16)'s middle at the frame's middle (512,384),
  and a press there with the hand empty arms the path tool red (mode 1, `d48` 1, read with the button held); its
  release refuses, and `original/capmatch.py` found `blnl_error1` at 0.558 there, the capture's only window over 0.3.
  With nothing armed after the menu the preview's cell reads (0, 0), so
  `cellfind.py` finds nothing there: aim with the camera instead. **A laying click's sound in the original** (Q632,
  `q632/orig/run.sh`, `BRK=1`): the same aim, `setcam.py` moved to the second cell between the clicks. A 123 ms ui clip
  scores only 0.29 to 0.35 in the mix (0.16 to 0.19 quiet), and a look-alike sample nearly as high, so name the effect
  by `brk.sh 0x0051bfc0` with `BRK_COND='*(int*)($esp+12) == 0x65'`: one hit a click, the first stack word the caller's
  return. `oneshots.py` does not see a ui effect at all (bit `0x1`, below). The queue tool there (Q671, `q671/orig/run.sh`):
  `setcam.py` over the Belly Bounce's queue cell (51,22) and a click at the frame's middle hands it over (mode 3, anchor
  (49,22)), then `setcam.py` over (47,22) and a click joins the path; here `q671/confirm.py` clicks the same cells. **The refusal latch a tool leaves** (Q532,
  `q532/orig/run.sh`, `q532/confirm.py`): in the original, `setcam.py` to (49,24), click (the path tool armed and
  anchored), `setcam.py` to (51,24), click (refused on the Belly Bounce), then Ctrl+S and the mechanics' button, read
  `[0x00816d50]` and `squares.py`; here `cellprobe.py`'s `find` arms `tool path`, and each probe's pass moves the latch,
  so probe a cell outside the park last (`tool` then reads `latch 0`), and find each cell at the camera it is clicked
  at: (51,24) and (49,24) at `camera 495 230 90 0`, (43,16) at `camera 475 190 90 0`, not the other way.
  **The queue tool in either game** (Q538, `q538/orig/run.sh`, `q538/confirm.py`): a click on the Belly Bounce's queue
  cell (50,22) with the hand empty edits its queue, mode 3 anchored on its last cell (49,22) (here `tool` says so; there
  `[0x0081ae2c]` 3 and the anchor `[0x0081ede4]`, `[0x0081ede8]`), and the pass runs every tick over the pointer's cell,
  so a hover alone sets the latch: (50,22) the queue's middle, (48,22) the join, (49,17) type 7 land. Type 7 land there
  is NOT outside the park (flags 0: `cell 49 17`); `parkfile.py -n` lists where a NOMODIFY path meets land that is
  ((48,17) and (48,16), flags `0x4c0`). Here (50,22) and (48,22) are found at `camera 495 225 90 0`, (49,17) and (48,17) at
  `camera 485 175 90 0`: set the click's camera back before the click. A console scene (`tool queue 13`, a click to
  anchor, `hover X Y`) must be ended with Escape while `hover` still holds the cell: released first, the pass runs over
  the pointer's own cell and moves the latch.
  **The locator's edges over a tool's strip** (Q540, `q540/orig/` with `lib.sh`, `q540/confirm.py`): commit a fence
  for Mike Cooper over the Belly Bounce (his window, the switch up then down, (52,26) anchored, a click on (54,27)), edit
  the queue, then Ctrl+S and the mechanics' button: the queue tool stays armed in both games and the edges are drawn
  over its strip. In the original they are drawn only while the follow runs (`setcam.py PID off` takes them away, `on`
  brings them back, the pointer kept); the follow holds the zoom, and the pick does not reach through the locator's
  panel, so (48,22) cannot be pointed at there: use the join (49,28), north of the anchor. Here `strip_end_at` finds it
  under the follow. In the original's mode 0 the preview's cell does not move: find cells with a tool armed.

- **The staff window's FIRE and PICK UP in either game** (Q432, `q432/orig/run.sh`, `q432/confirm.py`): on the
  original's 1024 x 768 frame `b_fire` is (228,385) and `b_pick` (308,385); FIRE's box puts its tick at (710,282) and
  its cross at (714,326); here they are `control 50c`, `50e`, `9873c9` and `9873c8`. Fire the stock park's one mechanic
  last: with him gone the mechanics' button opens a locator with nobody, and the strip opens no window. The gadget's
  money readout moves with income every second, so a cost is read by `original/bank.py` either side of the click, and
  here by `money`'s `staff` total. The strip's click folds the locator and ends the follow, so the camera stays put while
  the member walks on with the window up: a frame from before the strip's click is not the view at the pickup. Shoot
  the window, the carry and the drop, which share a camera.

- **The shop's window in either game** (Q31, `q31/orig/run.sh`, `run2.sh`, `q31/confirm.py`): the stock park at camera
  (480,265) 130 180; in the original **align the pointer before `setcam.py`**, as the align's sweep to the edges scrolls
  the park (round a's miss), then the Drinks Shop is (799,675) of the 1024 x 768 frame, the amount's thumb (483,338), the
  quality's (265,345), the price's plus (786,330), the door (766,434), OK (856,431); the window's buffered quality,
  amount and price are `0x007cc27c`, `0x007cc280`, `0x007cc284`, the shop `[0x007c2658]`. A drag there is `xdotool
  mousedown`, a `gmove.py` glide, `mouseup`. Here `shopwindow` is the census, `control c066` the root (a stream point
  maps through its rect, `confirm.py`'s `vpix`), and a drag is XTEST press, ten motions, release. The original's fast
  clock (NOT TRUE) turns a day and sells a drink within the run: read its figures from the opening frame.
- **The staff member's window in either game** (Q431, `q431/orig/run.sh`, `q431/confirm.py`): Ctrl+S, the mechanics'
  button (360,613), then the strip (380,505) of the original's 1024 x 768 frame opens it on Mike Cooper; its `b_ardown`
  is (810,182), `b_arup` (816,138), `b_locate` (809,231), `b_allstaff` (809,284), the preview (259,206), OK (816,378),
  and the all-staff list's first row (500,222). Here the console's `staffwindow` is its census and `control 50f` (the
  stream's ids) its places; OK's id is -1, which `control` does not parse (and the gadget holds -1s too), so click it by the root `50a`. **An advisor
  line's fate depends on how long a window stays up**: a Tutorial line said to its end is refused for ever after
  (`SayOnlyOnce`), one cut three times too (`DiscardAfterSlaps`); hold a window longer than the original's run did and
  the later posts differ (Q431's round 2): `confirm.py`'s `WAIT=0` keeps the original's pace.

- **The visitor's window in either game** (Q430, `q430/orig/run.sh`, `q430/confirm.py`): opened from the strip as below;
  in the original's 1024 x 768 frame its `b_arup` is (816,138), `b_ardown` (810,182), `b_locate` (809,231), `b_kids`
  (809,284), the preview (294,199), and the visitors list's first visible row (503,171); here the console's
  `visitorwindow` is its census and `control 3720` (and the rest of the stream's ids) its places. **The run log writes a
  line's time and "PM" with U+202F, a narrow no-break space**: a pattern on the log's clock needs `\s`, not a space
  (two of Q430's checks found nothing for it). A guest who has left changes which row of the list is first: read the
  `Visitors list: sort` line before naming one.

- **A person's window in the original, and the locators in either game** (Q429, `q429/orig/run.sh`, `q429/confirm.py`):
  off-screen after `loadfile.sh load`, Ctrl+V, the visitors' button (359, 641), then the strip (380, 505) of the
  1024 x 768 frame opens the visitor's window; its OK is (816, 378), as the staff member's (Ctrl+S, the mechanics'
  button (360, 613), the strip). The window folds the arm, so the locator is closed after it. In either game, Ctrl+S
  with the visitors' locator still out leaves the staff locator closed and its buttons find nobody: press Escape first.
  An advisor message's row in `q177/skeptic-analyser/meta_full.json` is at `0x76e300 + id x 56` (the response at
  `+0x24`), then `lib/responses.py <response>` for its sample; `lib/advisorrow.py <id>` does both.

- **The pointer on a person in either game** (Q447, `q447/confirm.py`, `q447/orig/run.sh`): a locator's follow keeps its
  person at the middle of the picture, so the pointer there is on them (here 0.5, 0.5 of the window, `pick` answering
  "standing thing N"; in the original (512, 384), scanned outward until the hover's category `[0x007c24f4]` reads 7 for
  a guest or 8 to 12 for a member, the thing `[0x007b05ac]`). Here `pointer` replies the cursor, the help row and its
  text filled; pause first so the person stays on the cell the pick asks. **The original's row is the foot bar** (288 to 736 across, 722 to 758 down of
  1024 x 768; crop it from the frame), and a modifier is held through its hover with `xdotool keydown Control_L` and
  `keyup` seconds apart, here with `key_down`/`key_up` (Q467, `q467/orig/run.sh`, `q467/confirm.py`).

- **Two staff stood near, and the pick over them, in either game** (Q628, `q628/orig/sweep.sh` with `score.py`,
  `q628/ours.py`): in the original `loadfile.sh load` the stock park, `staffpair.py` until two staff stand together (9 s
  in round c), then `memhold.py` holds the clock and a `spritepick.py PID 0.05` one-shot after each `gmove.py` step reads
  the game's latch beside the decode's (the pointer lands about 3 px up and left of the aim; judge by the poll's own box,
  not the aim). Here, paused at once, `pickup 25`, `putstaff 42 21`, `pickup 28`, `putstaff 42 22`, `camera 425 205 110
  0`, `step 1` stands Duke Mighten and Mike Man a cell apart in view, and `pick X Y` on a grid answers each point's latch
  without moving the pointer; leave out the steps within 3% of an edge, where a tenth of a pixel decides.

- **The pointer over a ride, a shop and a queue in either game** (Q478, `q478/confirm.py`, `q478/orig/run.sh`): the
  stock park at camera (480,265) 130 180 (`bothcam`); here `parkscene.find_cell` finds the queue (50,22), the Belly
  Bounce (52,24) and the Drinks Shop (43,31), a real move, then `pointer` (cursor, row and text) and the log's `Park:
  hover row 444`/`550` lines; `bothcam.to_original` gives the original's pixels, where one `xdotool` jump on `:77` and
  `tpwmem.py read` of the category `[0x007c24f4]`, the cell `[0x007b05cc]` and the thing `[0x007c24f8]` read the
  same cells, and the frame the bar and the pointer. The shop is aimed at (43,31), its cell furthest from the Litter Bin
  (44,29), which the first round's pixel (at the clamped zoom) met. The reference install's rows are `Language/american`'s ("edit this line").
- **The pointer over a window's model preview in either game** (Q634, `q634/confirm.py`, `q634/orig/run.sh`): here the
  stock park at camera (480,265) 130 180, paused, the Drinks Shop and the Belly Bounce clicked as Q478 finds them;
  `preview 0.6` holds the turning model (the ride's window and the shop's), `place c06c` / `3e24` is the panel, and
  `pointer` names the control under it. The arrow's own test is the frame: the share of the cursor picture's opaque
  pixels (`Cnor.tga`, its four frames, the best kept) found within 40 of their colour in the 32 x 32 box at the pointer
  (1.00 drawn, 0.00 covered); a shot with the pointer above the panel says the model fills the box. In the original
  the pointer is in the captured frame (its cursor library draws it into the back buffer): Q31's shop pixel and Q478's
  ride pixel open the windows, (270,183) of the 1024 x 768 frame is the preview's middle, OK (856,431) closes each.
- **A carried thing's squares in both games** (Q633, `q633/orig/run3.sh`, `q633/confirm.py`): the stock park at camera
  (470,270) 125 180. The original: Ctrl+click on the stock Belly Bounce (51,23) puts another in the hand (mode 4, Q635's
  route), `cellfind.py` with `PICK=1` aims at a cell, `squares.py` reads the fourteen squares (texture and turn) and the
  cursor word `[0x007cb2d8]` reads 7 (`c_place`) or 8; a right click lets it go. Here the cells' pixels are found by
  `cellprobe.py`'s `find` under `tool path` first (with a thing in the hand the drawn census starts at the centred anchor,
  not the pointer's cell), then `carry 1100`; `strip` with no cell prints the carried squares (`x,y,marker^lift`, a turn
  other than north's as `@bit`) and `| glide dx,dy`, which under `pause` holds the distance the squares still have to go.
  A real click puts the thing down centred on the pointer's cell: `world click: under (48,31): ... at (47,29)`.
- **A message box in either game, and its cross** (Q410, `q410/confirm.py`, `q410/orig/run.sh`): here the console's
  `windows` ends "MessageBox" while one is up (the reply begins `[dbg] windows:`), and `control 9873c8` finds its
  bottom place to click; R and the gadget's Research button (`control 2b`) open one in an Instant Action park. In the
  original, off-screen after `loadfile.sh load`: R (`xdotool keydown r`/`keyup`), the Research button at (166, 656) and
  the box's cross at (714, 326) of the 1024 x 768 frame close it again; B opens the buy screen for a park screen to close.
  Every box logs its words as it opens, `Message box: <text>` with each line break as ` / ` and `, with a tick` when it
  has one (Q143). In the lobby, `control 7a16` is slot 1's delete button: its click opens the delete box, and its cross
  closes it deleting nothing (`q143/confirm.py`).
- **The hire screen's account in both games, and a month turned** (Q385): this game's `money` prints the analyser's
  money months (`month balance N slot S cashin T/N staff T/N costs T/N slot S ...`, the month so far over the newest
  closed; the balance ring has no month so far, Q387); the log's
  `Hire screen: opened, ...` and `Hire screen: candidate ...` lines and the training screen's `account a / b / c / d`
  are the figures. The park sweeps four times a second, so the stock park's first month here (near tick 1470) is
  about three minutes after the pause is lifted: wait for `Park: analyser month closed` up to eight minutes. **The
  gadget's Buy button reopens the screen it was left on**: after the hire screen it opens the hire screen itself, and
  a b_allstaff click there lands on its b_allthings. The original's figures are `tpwmem.py read 7ca2f0:i 7ca2fc:i
  7ca2f8:i 7ca300:i` (cash in, staff, all costs, other) and its rings `thingwords.py ... 5:0x1fc90:i 5:0x1fed4:i
  5:0x1f7f0:i 5:0x1f5a0:i 5:0x1f7e4:i` (`q385/orig/run.sh`). **A file this game saves after its own first month is past
  the original's month too**: the original closes a month on the load's first tick, so its screen shows what the file
  held as the month so far.

- **Does the original's draw use this at all** (Q361): before poking a texture's or a material's flags to see what
  changes, read-watch the word (`WATCH=rwatch watch.sh ADDR 4 4 15`) beside a control that is read (`mGameTick`, or a
  texture of the same model that is drawn). A poke of a word nothing reads changes nothing and proves nothing; the
  floors' did, two rounds running. Find the word by its pointers: `texcache.py` for the texture record, `pointsat.py`
  up to its material slot, material object and source record (`floormats.py` does all three).

- **A sign's board beside the original's** (Q357): Lost Kingdom's park gate, from inside the park. This game:
  `camera 480 140 80 180` and `step 2` under `pause`, saved; the original given that file through
  `original/screenpark.sh` frames the board alike since Q441 (one lens: "Cameras in both games"); any difference left is the script's own framing (`screenpark.sh` frames the camera's own point, Q359). Re-check `q357/measure.py`'s patches, which were placed for the two scales, before reusing it.
  `q357/measure.py <frame> orig|ours` reads two lettering-free patches of its face and the slab's top. The lobby's
  boards are no use: in the original's orbit the player buttons cover them, and this game's `island N` frames
  them small and mid-flight.

**In OpenTPW, through the console** (`OPENTPW_DEBUG_CONSOLE=1`; the `case` labels in `DebugConsole.cs` are the
whole list):

- **A fresh Full Simulation park, in both games** (Q349). This game: `lib/freshpark.py` (a player folder with no
  `gms.dat` is a new Full Simulation player); the park's first log line is `jungle: fresh park: ...`, the poll
  starts on tick 1, so pause at once (tick 8 on the desktop and off it), and row N's first turn is its place
  among the polled rows (row 80: tick 50). The original: a folder in the reference install's `save/users/` under
  a free slot digit, holding a Full Simulation player's `gms.dat` and empty theme folders; two clicks on its row
  of the player list (660, 158 for the second row) go straight into Lost Kingdom, which it makes fresh and
  writes as `jungle/restart.INTS` within the minute. Remove the folder afterwards (`original/freshfs.sh`).
  **A Full Simulation scene both games load** (Q327): in the original's fresh park, off-screen, its buy screen (Buy
  (117,588), the misc tab (843,123), a row at y 261 + 19.5k, then the ground: one feature a row, the hand empty after
  each) and its hire screen (the side tab (957,316), the researchers' tab (843,121), a candidate's row, then the
  ground) build the scene; Escape, Save (510,128), a name typed with Shift held for capitals, Return writes it to
  `2fsfresh/jungle` (copy it out before `freshfs.sh stop`). The file, edited if need be (`parkfile.advisor_forget`;
  `mParkClosed` through `pk.Park`'s `hdr['closed']`), is loaded in the original by `original/fsload.sh` and here
  with `lib/freshpark.py`'s folder holding it in `JUNGLE` before `enter()`, then `load(1)` (`q327/confirm-load.py`).
  The original's sweep ran about 6.5 a second and this game's 4.8: compare turns and order, not a line's length in sweeps.
- **Is a float product rounded before its compare** (Q343): break on the `FCOMPP` with `BRK_PRINT` (`brk.sh`'s
  row) as the `POLL` of a `loadfile.sh` load: `fctrl` 0x27f is 53-bit precision, so `FILD`, `FMUL float ptr` is a
  double of the float key (five times the float nearest 0.2 is more than one). The same hit gives the row's turn
  (its tick) and both sides of the compare.
- **A polled row whose shipped score is under the advisor's 25** (Q344, row 79's `Score` 20): neither game can
  say it as shipped, so both are run under a raised `Score` beside the key whose edge is asked (`q344/confirm.py`,
  `q344/orig/run.sh` with `POORER=`, `MORE=`, `SCORE=`); `Score` 25 is the control that the limit is strict
  (`result=under-threshold` here, a fresh record there). A whole-number share's edge is one key apart: 62 silent
  and 63 said pins five kinds of eight in both games. **The leavers' summons in this game is not on a fixed tick**
  (1093 in Q341's runs, 1139 in Q344's): a day-29 check asks whether a stamp came before the row's next turn.
- **A polled row whose first sayable turn is late** (Q345, row 81 on tick 1593): one scene run saves the park
  at each tick wanted (`q345/scene.py`: `save()` then a copy, as often as needed; a purchase and a sale between
  saves make the controls), so four files cost one run. **A spent bus is summoned again on a fixed tick of the
  stock park (1264, 1438, 1600 here) and both games may draw a ferry or seaplane there, which stamps the newest
  build**: read the scene run's `summoned` lines and save between two of them, never just before one that falls
  on the row's turn. A day-short file is seen said a turn later only in the original with its stamp held
  (`EXTRA="python3 -u .../thingpoke.py %PID% 150 13 5:0x21c30:Q=<stamp>"`). **A tag lives thirty seconds**: after
  `loadfile.sh` returns (some 20 s after the click) a `run.sh` that takes frames first clicks a tag that is
  ending; `WAIT60=0 SECS60=0 CLICKS=1` clicks in time, and `GRAB=1` has the frames of him speaking.
- **Four kinds of ride in an Instant Action park** (Q343): `stockscene.py ... 912 "buy 1101 41 22 0" "buy 1102 70
  40 0" "buy 1104 55 30 0"` (a Crazy Ape, a Rocky Racers and an Aztec Mayhem beside the stock Belly Bounce, each
  left shut as bought, which both games count as a kind standing). The Instant Action catalogue holds twelve kinds; four are on offer at the start and the lab can bring the rest. Made at
  765 (paused on 768) with `fire 30` first, the stock researcher dismissed, the file keeps the lab's eight ride
  projects and both games say the advisor's row 16 on tick 783 (Q421, `q421/all4.TPWS`).
- **A member of staff's grade written in a file, and the second tag's click** (Q350): `parkfile.thing_set( src,
  dst, 30, 398, '<i', grade )` writes the stock researcher's `mCurrentPayGrade`, which the original reads back at
  his `+0x1e4` (`thingwords.py ... 30:0x1e4:I`). **A stock park whose researcher is of grade 0 hears a training
  line on tick 1109 in the original**, so a line said after it waits some twenty ticks and its tag is the second
  on the mast: click it at game point (59, 433), not the first tag's (59, 471). A run that ends with a box or a
  screen open (the bar's research button leaves one) breaks the next `loadfile.sh` load: make it the chain's last.
- **The original's research screen in Full Simulation on the real screen** (Q705, `q705b/orig/drive.sh OUTDIR`): a
  fresh Full Simulation player made as `freshfs.sh make` makes one (Alexah's `gms.dat` in `save/users/2fsfresh`),
  `DISPLAY=:0 original.sh start`, then `screen.sh`'s entry (Q558): `gmove.py align`, (2540, 1420) and a click for the
  welcome, `align` again, the player's row (660, 158 of 1024 x 768, screen (1557, 296)), click, three seconds, click; 46 s
  later the fresh park. The file copied into that player's `jungle`, then (714, 326) for a box, Escape, Load (510, 50),
  the row (400, 123); R opens the screen. A thumb is held with `xdotool mousedown 1`, glided with `gmove.py`, let go
  with `mouseup`; `respump.py`, `musicvoices.py PID SECS 154,155,156` and `mixfreq.py PID SECS 2d` read it. The player
  folder removed and `save/` summed after. Without the welcome click the first run sat in the lobby (`orig/a-nopick`).
- **A research project finished on demand, in both games** (Q702): `parkfile.thing_set( src, dst, 6, 16 + 16 * n +
  12, '<f', points )` writes the stock lab's (thing 6) project n's `mResearchDone`; Dino Karts is project 1 and takes 6
  a hand-out, so 576 finishes it on the fourth hand-out after the file's tick 755, on 820 in both games
  (`q702/dino576.TPWS`). Here the file is the private player's newest jungle file, entered from the lobby; in the
  original `loadfile.sh` loads it. The analyser's ring of tab t is `thingwords.py %PID% SECS 5:0x1f068+0x14t:H:temp
  5:...+2:H:d0 ... 5:...+8:i:cur 5:...+0x10:B:wrapped` (thing 5 in the stock park); `advisor.py %PID% SECS 0x5d,0x85`
  shows the finish's line said and the raise's post at -1. The buy list's state heading is layout point (1696, 368),
  the original's (848, 184) of 1024 x 768: a click sorts by the state ascending, a second descending; the census is the
  log's `Buy list: tab 0, sort 3, ...`. The advisor's sample is `mixmatch.template( speechHD.SDT, sample - 1 )`
  (`sp_282.mp2` is index 281) matched over the whole capture.
- **The end of research on demand, in both games** (Q550): `q550/mkscene.py` writes every project of the stock
  lab but Dino Karts at its cost onto `q702/dino576.TPWS`; only the rides' kind is then funded (10.2 a hand-out), so
  the third hand-out after tick 755, tick 800, finishes the last project in both games (`labsim.py` agrees). **The
  original enters the park on the newest file of the player's jungle folder as it starts**, so a file that ends
  research at once must not be the one it starts on: start it with `loadfile.sh load q702/dino576.TPWS` and then load
  the scene into the running game (`q550/orig/run.sh`). The End feature then takes the keys; `xdotool key Escape`
  ends it and leaves UITEXT 471's box. Here, back the private player's `gms.dat` up and put it back after a run that
  saves (`q550/confirm.py`), or the jungle record stays marked. In Full Simulation (`gmsfile.put( path, mode=0 )` on
  that backed-up file, `q550/confirm-fs.py`) a finished ride appends its upgrade from the items' own files, which
  kind 4's nought effort never funds: end on a feature (`q550/stone240-fs.TPWS`, Stone Head at 240, tick 800). The
  End's view itself, with the camera and the layers read, is `q551/orig/run.sh` (`original/camread.py`; it waits for
  the End's id and frames every 2 s, moving the pointer). Ours: the console's `endfeature` is the End's census (its
  thing, its two scripts, the eye and the look, the cover, the dip), and `q703/confirm.py <out> <bin> a|b` runs the
  scene through the End to Escape (`a`) or to a right click (`b`). First person's fog, drawn in the End's view too,
  hides what stands past about 150 units: `fog 2000 2100` pushes it out for a look (`q703/exp-fog.py`). Its planes' flight
  (Q554): `endfeature` adds an `endfeature planes:` line, each `_f_endplane`'s drawn place and whether it is shown;
  `q554/confirm.py <out> <bin> a|b` samples it every ~0.7 s and frames every ~1.4 s (`b` with the fog pushed out), and
  `q554/orig/run.sh` films the original's End every second. Endm1's three come over the view about 4 s in. Their trails
(Q555): an `endfeature trails:` line, each particle of the two scripts as `script:effect@node (x, y, height)` and how
many moves the sweep has made (`carried`); `q555/confirm.py <out> <bin>` checks plane 02's two against it.
Its trails' density (Q703): `q703d/orig/run.sh` films the original's End with `original/emitters.py` (the live density,
each trail's rates, most and count) and `particledump.py` of 93 to 95 at each frame; `q703d/ours.py <out> <bin> main|fix
[sizes]` reads the console's `particles` (density, rates, most, counts) every second and frames every two, `sizes` adding
a `particles <slot>` dump of one live trail of each effect, one dump a loop (take it only from one holding 90 or more,
and by the effect its own header names: a trail its clip has killed holds only its old, large stars, and a slot the
census named may hold another effect by the dump). A trail's plateau is its largest count: a killed one empties
by 30 or more between samples. The original's frames start only when `loadfile.sh` returns, some 14 s
into the End, so pair the two games' frames by the clip seen, not the clock (`q703d/side-by-side.png`).
Its sounds (Q553): `q553/orig/run.sh` runs the original's End with `original/voices.py` and `mixer.py` beside it (the
End's voices and their channels; for its first ~7 s a volume of 100 reads 28 of 127, then 75), `q553/confirm.py <out> <bin>`
runs ours with the mix to a file and the console's `scriptsounds` polled (every script voice: its type, effect, class,
sample, volume, place, distance, range and gain), and `q553/mixcheck.py <run>` finds the End's samples in that mix. 200,
201 and 202 play at a drawn pitch and match weakly at pitch 0: read their loudness from the census, not the mix.
The advisor over the End (Q556): `q556/orig/run3.sh` films the original's End at 15 fps from before the click with
`original/advdraw.py` beside it; take a frame at a `drawn` line's wall clock less the film's `.t0`. Row 167 is drawn
about 7.7 s to 14 s after the poller starts and row 93 about 16 s to 22 s, and **`loadfile.sh` returns about 23.5 s
after its poller starts**, so a key meant inside either line cannot be sent after it: send it from the `POLL`.
- **A park bigger than its handymen, or its guards** (Q411, Q412; `lib/pathscene.py`, Q411's `q411/mkscene.py` made it
  first): the stock park paused on 765 and `path X Y` given for the first N cells of the 17 x 17 bare block at (60,15) (`parkfile.bare_blocks`), each
  with a 0.15 s wait (`stockscene.py`'s 2.5 s a command is twelve minutes for 289), then saved: 126 + N developed
  cells (`parkfile.py -d`). **A reply can come after its 0.15 s window**, so count the cells in the file, not the
  replies. The advisor's row 10 then turns on tick 777 in both games; one handyman keeps 400, so 289 cells (415) say
  his line and 286 (412) do not. Row 13 (Q412): `pathscene.py <out> <bin> big.TPWS 1230 374 -- "fire 28"` (500 cells,
  the shipped guard dismissed); it cannot score before tick 1400 (30 days since the researcher's hire on 648), and
  both 500-cell files make row 10 speak first, on 1242. **The poll writes a row's line only when its answer
  changes**: a control that stays `no-score` logs its first turn alone; read the `advisor` census for the rest.
- **A park with no guard and misbehaving kids** (Q413): `pathscene.py <out> <bin> noguard.TPWS 760 0 -- "fire 28"`
  (N may be nought: the guard alone dismissed, paused on 765), then this month's kids, caught and not, and ever written
  with `parkfile.thing_set( src, dst, 5, 73468 + 4 * i, '<i', v )` (`q413/mkkids.py`; `parkfile.py -s` prints them).
  Both games read the month's two at a load; the advisor's row 12 then turns on tick 779, four kids saying it and
  three not, and the original also posts row 264 on 903 for more than two. **A still taken after `loadfile.sh` comes
  too late for a line said a few seconds after the load** (the original off-screen): start `record.sh` as the load's
  log appears and pull the frames. Row 264 (Q414) takes the same files, both games saying it on 903 in the guards' cap
  for three kids and silent for two (`q414/two.TPWS`). **The original's pollers (`advisor.py`, `tags.py`) log a line only
  on a change**, so a wait on a logged tick past a quiet turn never ends: wait on wall time (about 3.8 ticks a second
  off-screen, the load's clock starting about 0.7 s into its log) or read the date on the end frame.
- **An object's money and satisfaction days written in a file** (Q368, `q368/mkscene.py`): in an object's record the
  costs ring is at 228, the takings ring at 361 and the satisfaction ring at 901, each the entry, the count, the wrap
  byte and today (13 bytes) then thirty days, so day `k` is `thing_set( src, dst, t, ring + 13 + 4 * k, '<i', v )`;
  `mTotalCosts` is 1086 and `mTotalTakings` 1090. Write only slots the ring has filled (`parkfile.object_days`'s
  entry and wrap), and slots the original's day rolls will not reach before the frame: it rolls a day about every
  five seconds after a load. `original/money.py` reads the result in the running original.
- **Every kind of staff written untrained, a training budget in a file, and the top tag of several** (Q351):
  the stock staff are things 25 (handyman), 26 (mechanic), 27 (entertainer), 28 (guard) and 30 (researcher);
  `thing_set( src, dst, 1, 82 + 4 * kind, '<i', budget )` writes the staff HQ's `mBudget[kind]` (0 handymen, 1
  mechanics, 2 entertainers, 3 guards, 4 researchers), which the original reads back at thing 1's `+0xc + 4 *
  kind` (`thingwords.py ... 1:0xc:i`). With all five at grade 0 both games say five lines from tick 1105 to
  about 1269 (41 s), and **a tag lives thirty seconds, so the first is gone before the fifth is said**: click
  within a second of reading where the tags stand, never after another console call. The newest tag is the
  topmost: in the original at game point (59, 471 - 37.5 x (held - 1)), here from `tags`' topmost `tagNNN=`
  rectangle, its middle over four times 0.9375 (`q351/confirm.py`, `q351/orig/run.sh`). Take a tag's words
  from `strdump` on the folder the game under test reads: strings 34 and 37 differ ("cleaners" and
  "scientists" in English, "janitors" and "researchers" in american). A run whose pollers see no change prints
  no tick after the load: give `thingwords.py` a word that moves (a mechanic's state, `26:0x19c:I`).
- **A month closed in profit or in loss in the original, on demand** (Q641, `q641/orig/run.sh`): after a
  `loadfile.sh` load, write the analyser's month so far once with `thingpoke.py PID 0.2 13 5:0x1fc90:i=10000000` (cash
  in; thing 5 is of type 13) or `5:0x1f5a0:i=...` (all costs), then `waitdate.py PID M 1` and `waitdate.py PID M 2` for
  the close and the day after. A closed month's own sample is written the same way at `+0x1fc94 + 4 x index`
  (`+0x1fed4` holds the index; costs `+0x1f5a4`, `+0x1f7e4`): nothing writes it again until the next close. A POLL
  started before the load click can write February's before 1 March, which `loadfile.sh` itself leaves too little time
  for. Stop the original afterwards. Q641
- **The same in this game, and the balance at any width in both** (Q674, `q674/ours.py`, `q674/orig/run.sh`): here
  `money cashin N` and `money costs N` write the analyser's month so far and `money balance N` the bank's balance (the
  console's instruments; `money` says `trend red|green`, `control 31` and `control 32` the arrow's and the icon's `frame`);
  wait for the log's `Calendar: 2000-03-01` line and `pause`. The original's balance is the bank thing's `+0xc` (its id is
  the world's `+0x1da726`, 8 in the stock park, its type 16): hold it with `thingpoke.py PID 6 16 8:0xc:i=N`. **The original runs at
  800 x 600** (its `Resolution.sam`) scaled up to the 1024 x 768 capture, so its lettering is the 600 set's: compare with
  this game in a window of 800 x 600 (`size 800 600`) scaled by 1.28, never with a 1024 x 768 one. A park file whose
  arrow differs from the running one's isolates the load's own call: `q674/mkfiles.py` writes a closed month with
  `parkfile.thing_set`. Q674
- **The original's Financial Information screen, and a ring's months on it** (Q386, `q386/orig/run.sh`): F in a park
  opens it on the bank balance alone, 1 Year (12 points). Its rows at game points (252, y) on the 1024 x 768 frame: Bank
  balance 122, Park value 159, Money in 208, Gate takings 245, Shop takings 283, Sideshow takings 320, Money out 358,
  Staff costs 396; a row of the money group clears the balance and park value. The graph labels only each line's
  NEWEST point; read a month by its height over the baseline (y 389), the tallest line's peak at y 150. Thing 5's rings
  poll with `thingwords.py` (`5:0x1fee0:i` a total, `+0x244` its slot, `+4 + 4 n` month n).
- **The Staff Training Budgets screen, in both games, with no tag** (Q352): the gadget's Money button, then the
  entry-price screen's staff-costs button. Here: `control 2a` gives the Money button's place, then points of the
  2048 x 1536 layout through the screen's own scale (`q352/confirm.py`'s `topix`: a wide window centres a
  screen): the staff-costs button (1619, 354); on the screen a row's minus at x 955 and plus at x 1316, the rows'
  middles y 254, 409, 563, 717, 871; the entry button (1739, 626), the tick (1752, 861). The original
  (`q352/orig/run2.sh`): the Money button at game point (107, 645), the staff-costs button (810, 177), a row's
  minus (478, y) and plus (658, y) with y 127, 204, 281, 358, 435. `budgets` reads this game's five, the flag and
  the screen; `thingwords.py ... 1:0xc:i 1:0x10:i 1:0x14:i 1:0x18:i 1:0x1c:i 1:0x20:B` the original's. **Opening
  the screen writes every budget back, so it sets `mHaveEverTrained` by itself**: read the flag before it. A
  tag's count taken from the log before the load's click is stale (`run.sh`'s `held`): click a tag only after a
  fresh `tags.py` line. Both games' months turn on the loaded file's calendar (Q149).
- **The harbour's sea at one phase in both games** (Q490): this game `pause`, then `sea phase P` (the console's `sea`
  prints the corner, phase, folder and words; `sea y H` sinks it to read `Line01` alone); the original
  `memhold.py %PID% 4 0x00871f60:f=P`, a frame grabbed 2 s in (`q490/orig/run.sh`). The same P draws the same wave in
  both (the words differ, being random); P and P + 4π are one wave apart (mad 0.00 in both), P + 2π half. At camera B
  measure beside the seaplane, which parks over the first sea box in ours: the original's (100,150)-(250,300), ours
  (254,141)-(394,281). Two frames meant half a wave apart are grabbed by wall clock, 0.52 s grab to grab: `shot()`'s
  settles put them about a whole wave apart.
  **A sea's colour is the eight phases' mean, P = 1 + k 4π/8, k 0 to 7, in both games** (Q700, `q700/ours.py`,
  `q700/orig/run.sh`): the words are drawn afresh each run, so a k0 frame differs between two runs drawing the same
  pixels (main twice 4.4; main and the branch at TEXTURE32 1, 1.8), while the eight's mean holds to under 1 a channel.
- **This game at the original's look: four bits a channel, 16-pixel tiles, stepped pumps, jumping flags, letters at 1** (Q616,
  Q645; the improvements are the default): `gamefolder.make( DEST, {} )`, then append `graphics faithful` to
  `DEST/save/opentpw.cfg`, and hand DEST to `parkrun.py` as `GAME_FOLDER`; the run's log says "Graphics: opentpw.cfg's
  "graphics faithful"" and ""graphics faithful" and med.sam's TEXTURE32 0, so the game's art is cut to four bits a channel".
  With `data/med.sam` changed too (`gamefolder.py DEST data/med.sam "GraphicalOptions.TEXTURE32<six tabs>0" "...1"`) it
  keeps eight, as the original does (`q616/run3`). `q700/ours.py` and `q700/measure.py` shoot and measure cameras A and B.
  `q645/ours.py <out> <bin> <ROUND> faithful|improved` makes the folder and shoots the gate pair, the flags' turn and the
  buy screen's text census in one run; `q645/pumps.py` films the pump in it. Under the console's `pause` the faithful
  flags take a `weather N` change on the next drawn frame (their factor is 1 whatever `Time.Delta`): measure the turn
  against the angle before the call, not the gap left after it.
- **The gates' flags at one wind and one phase** (Q491): `thingpoke.py %PID% S 15 7:0x5c:f=W` holds the weather's
  `mWindDir` (every flag turns with it; 0 streams them to +x), `memhold.py %PID% S 0x00871ed0:f=P 0x00871f60:f=P` both
  phases. **A read under a held phase can be a frame late**: the frame steps the phase 0.4 before `memhold.py` writes
  it back, so a `dynfaces.py` dump is laid at P + 0.4 now and then (`q491/orig/b-p15.0.json`): take five reads 0.1 s
  apart and keep one the model fits at P (`q491/orig/run-c.sh`). The flags' geometry repeats every 20 of phase; P and
  P + 10 differ in y alone. At camera B the nearer flags' boxes (395,435,500,485) and (750,435,870,490) are clear of the
  sea; this game's are (530,408,629,455) and (863,408,976,459). Those boxes hold the flags at wind 0 alone: at the winds of 90 to 176 the original's stand in about (500,330,590,550) and (800,330,1000,530), this game's in (615,315,725,465) and (900,315,1080,490) (Q643). This game (Q493): `pause`, `sea phase P` (the flags wave
  at the sea's phase, so it moves both), `flags wind W` holds the wind (`flags wind free` lets it go), `flags` prints
  the count, phase, the drawn angle `at`, the weather's wind and `Time.Now` (`q493/ours.py`). **The eased turn, frame by frame** (Q643): `pause`, then `weather N` moves the weather's wind and not the flags (retry for a turn of 15 or more), then `step 30` and so on, each followed by `flags` and `sea phase 5` before a shot: frames of a sixtieth, so the gap is the model's own (`q643/ours/confirm2.py`).
- **This game at another graphics quality** (Q482): `gamefolder.make( dest, {} )`, then write the quality into the
  folder's own `save/Config.tcf`, the dword at byte 12 (0 low, 1 medium, 2 high; `ConfigFile.Record`'s order after the
  version: card rendering, resolution, quality), and hand the folder to `parkrun.py` as `GAME_FOLDER`. The level reads
  `low.sam`, `med.sam` or `high.sam` by it as the park loads (`q482/ours.py`, `GAME_FOLDER=... run2-low`).
- **A balance key changed in both games** (Q338): this game from a private folder (`lib/gamefolder.py`, then
  `GAME_FOLDER` before `parkrun.py`; an off-screen run wants a folder of its own beside the desktop's). The
  original reads `~/Games/TPW-original/Data/Advisor/Advisor.sam` as it starts: stop it, check the file's sum is
  the shipped one, keep a copy, write the key, run, stop, put the copy back in a `trap` and print the sum
  (`q338/orig/run.sh`, `SAM=`). The loader takes a float as written (`ShopsPerRideRatio 1.5`). A run in which
  nothing is said prints no line after its first: add `EXTRA='python3 -u .../thingwords.py %PID% 60 29:0x8:H
  35:0x8:H 31:0x8:H'` so guests' places stamp its ticks.
- **A row that waits thirty days from the newest build, in the original** (Q340): its stamp
  (`thingwords.py ... 5:0x21c30:Q:lastbuilt`) moves with nothing bought near tick 1070 to 1140 and again some
  340 ticks on, in every load of a stock-park file, so a turn after tick 1069 is not said there. Either put the
  turn before it (Q335 to Q339's turns, 957 to 967, are), or hold the stamp at the file's value for a
  like-for-like and say so: `EXTRA='python3 -u .../thingpoke.py %PID% 110 13 5:0x21c30:Q=<stamp>'` in
  `q340/orig/run.sh` (the analyser, thing 5, is of type 13; `Q` works). Left alone it speaks on its first turn
  692 ticks past the last move (tick 2209 in `q340/late30.TPWS`: `SECS=370 WAITN=720`). What moves it is the
  ferry or seaplane made at its first summons (Q341), so twice a stock-park file and never after.
- **Who calls a routine, and when** (Q341): after a `loadfile.sh` load, `brk.sh 0x004c7680 2 240` waits for the
  routine and prints the tick and the return addresses on the stack; look each up with Ghidra's
  `decompile_function_by_address` (the one after a `CALL` is real). What happens in the first seconds after a
  load is missed by the attach: read the next occurrence, or the words it changes with a poller in `POLL=`.
- **A row no shipped park can score** (Q339): change its key in both games (above) AND, where the count is of
  kinds on offer, mark items researched in the file with `original/research.py`: the original keeps a
  sideshow's researched byte as written, as a shop's (four on its "Buy Sideshow" list). Each run of the
  original on a changed `Advisor.sam` is a program of its own (about three minutes): put the silent ones in one
  background chain with `EXTRA=` `thingwords.py` on three guests' places, and read each log's last tick.
- **A park with a ride and no shop is the stock park with thing 16 sold** (Q337): `sell 16` under a pause takes
  the Drinks Shop, its only shop; `sell 13` takes the Belly Bounce, its only ride. A sale leaves
  `mLastObjectBuilt`, so the days since the build are still set by writing the stamp into the file
  (`parkfile.thing_set` on thing 5, `+73516`; `q337/mk.py`).
- **`step N` steps frames, not ticks** (Q334): twenty of them moved the park's clock two ticks. To let ticks pass
  under a script, `resume`, wait on `tick()`, and `pause` again.
- **Who writes and who reads a field of a big thing** (Q334): `search_bytes` on its displacement's four bytes
  (`30 1c 02 00` for the analyser's `+0x21c30`) lists every instruction that names it, in a function or not; a
  routine nothing is seen to call is called from undefined bytes, found by scanning `testme.exe` on disk for
  `e8` and a relative target (`responses.off( va )` turns an address into a file offset), and the caller's row
  of the advisor's table is the one whose score address is the nearest at or below the call.
- **A tired member of staff, from the first sweeps of a load** (Q326): write the member's rest into the file, the
  float at the staff record's `+499` (`parkfile.thing_set( src, dst, 28, 499, '<f', 1.5 )`), where the console's
  `staffrest` and a write to the original's memory each need a run already going. The stock file's guard 28 is
  idle on stamp 752, so he is tired on tick 763 in both games, is in the Staff Room on tick 891 and up again on
  1088 (grade 3, 0.5 a tick). His walk is 128 ticks, half a minute: a script that watches it needs more than
  150 s from the load to the rest's end. `thingwords.py %PID% 170 28:0x19c:i:state 28:0x208:H:restarea
  28:0x1fc:f:rest` and `object.py %PID% 170 20 0` (the room's `VAR_STAFFIN`) read the original's side
  (`q326/orig/run.sh`).
- **The speaker is free from the entry to a park or a load** (Q316): the stock park says no opening line, its file
  holding the control panel's line, message `0xb9`, as said, so a line posted in the first seconds is heard. The
  older scripts' 23 s waits after a load are no longer needed.
- **The control panel's line, on demand:** enter a file whose record of `0xb9` is fresh
  (`parkfile.advisor_forget( stock, out, 185 )`, copied into the player's jungle folder as the newest): he says
  sample 377 on tick 877 in both games, 30 s in, for 19 to 21 s (the log's `Park advisor: poll message=185 ...
  result=post`): the poll's cursor is the file's (row 277 in the stock file; `advisor.py ... poll` prints the
  original's). A park made with no file, in a fresh program, says it on tick 265:
  `SLOT_CONTROL = "7a19"` before `parkrun.py` enters as the second player, whose jungle folder is empty (`7a15` is
  the first player's name, not a slot); `tutorial off` in the console has the poll answer `tutorial-off`
  (`q316/confirm.py fresh|nofile|cutoff|early`, `q316/orig/run.sh`). In the original the Tutorial switch is the byte
  `0x0078d90d`: `bytehold.py %PID% 110 0x0078d90d=0 900 0x0078d90d=1` beside `advisor.py` holds it off through a
  load and until tick 900 (`q316/orig/run5.sh`).
- **The tips' line, on demand, a second after a load** (Q317): a file whose record of `0x11a` is fresh
  (`parkfile.advisor_forget( stock, out, 282 )`) has him say sample 559 on tick 760 in both games, the fifth sweep
  after the stock file's load, for 10.7 s. `loadfile.sh`'s own frames come 12 s and 21 s after its click, too late
  for a line that short: start a grabber of `:77` that waits for the `-1-load-list.png` frame and then grabs every
  1.2 s (`q317/orig/run2.sh`).
- **A message waiting in the advisor's slots, on demand:** `broken_pot()`, wait for the log's `attempt=N message=70`
  (the mechanic's line for the pot, 6.5 s long), then `scriptvar 13 VAR_BREAKSTAT 1`: the Bounce's `post message=69
  ... result=queued` waits until that line ends. `pause` and `savepark` inside it write a file with the slot taken;
  the original says it a tick after loading such a file, and `loadfile.sh`'s log shows it twice (it starts the
  original on the file, then loads it from the list) (Q314). Since Q315 the same file holds the line's wait too:
  both games then say the waiting message when the wait ends, about six seconds after the load, not a tick after
  it. `parkfile.py -a <file>` says how many milliseconds of the wait a file was saved with, and
  `q257x/orig/broken-by-the-original.TPWS` is a file of the original's own saved inside one (959 ms) (Q315).
- **A shut park, and the advisor's line about it:** `screen 10` opens the entry-price screen, `control 4f3b1`
  answers the door's place, and a real click there shuts the park (no console command does); Escape closes the
  screen. Done inside eight seconds of the stock park's entry (by tick 800), the guests still at the booths wait
  there for 200 to 349 sweeps, `gate` ends with `waiting N`, and the advisor's row 1 is polled on tick 923 (768
  and every 155 on): with two or more waiting he says sample 1 in the ticket hat and leaves tag 3, whose bar's
  first button (`click 265 549` with the bar out, the tag at `click 56 441`) opens the entry-price screen
  (`q319/confirm.py`). **A file of that park saved before tick 899 has both games say a line about shut shops
  first** (rows 260 to 262 are polled on 899 to 901: sample 487 on tick 899, then the toilets' and the sideshows'
  lines and row 1's in the order of their scores, each as the wait before it ends), which holds row 1's line back
  to tick 942 or later: save past tick 902 (`pause`, `savepark`) for a file both games say row 1's on tick 923 from
  (`q319/orig/run.sh <file>`, with `CLICKS=0` for no clicks; Q319). The four lines in both games:
  `q320/confirm.py`, `q320/orig/run.sh <file>` (Q320).
- **An open park with nothing to ride, and the advisor's line about it:** in the stock park `pause` on a tick
  from 912 to 919, `sell 13` (the Belly Bounce), `sell 14` (the Jungle Spray), `resume`: row 2 is polled on tick
  924 (769 and every 155 on) and he says sample 4 in the ticket hat and leaves tag 4, whose bar's first button
  opens the entry-price screen as tag 3's does. `savepark` under that pause writes a file the original says the
  line on tick 924 from too, and then row 62's (on tick 968; this game as row 2's wait ends, 961). The
  original's line lasts from 2.5 s to 13 s after the load's click, before `loadfile.sh`'s own frames: put
  `(sleep 6; import -window root -display :77 out.png) &` in its `POLL` (`q321/confirm.py`, `q321/orig/run.sh`
  with `GRAB=1`; Q321).
- **Worn rides and no mechanic, and the advisor's line about it** (Q331): the line needs six rides, so the scene
  is a file: `q331/scene.py <out> <bin> fired` (the stock park paused on tick 915, `fire 26`, `buy 1101 41 22 0`,
  `buy 1104 57 23`, `buy 1101 70 40 0`, `buy 1101 30 60 0`, `buy 1101 60 60 0`, `savepark`), then
  `q331/worn.py <src> <dst> 13=24 43=24 ...`, which writes each ride's State of repair (the float at an object's
  record's `+1074`) through `parkfile.thing_set`. Row 5 is polled on tick 927 (772 and every 155 on): with six
  rides under 25 he says sample 13 in the hard hat and leaves tag 7, whose bar's first button (`click 265 549`,
  the tag at `click 56 441`) builds the hire screen on its mechanics' tab (`windows` names the hire screen's
  tab). A ride at exactly 25, or the mechanic kept, and nothing is said. In the original `screens.py` prints the
  tab (`hiretab`); `loadfile.sh` comes back some twenty seconds after its click, so a frame of a line said three
  seconds in is grabbed from the `POLL` (`q331/orig/run.sh` with `GRAB=1`; `STOP=0` keeps the original up for
  the next file). The same file has both games say row 113's line, the unjoined queue's, on tick 996.
- **More rides than the mechanics keep, and the advisor's line about it** (Q333): row 7 is nought until thirty
  days after the newest hire, and the shipped park's newest is tick 648, so in a live park the line cannot come
  before tick 1340. To have it on a turn soon after a load, write the stamp into the file: the analyser is thing
  5 and its `mLastStaffHired` the eight bytes at its record's `+73524`, a `FILETIME` (`parkfile.thing_set( src,
  dst, 5, 73524, '<q', parkfile.filetime( tick ) )`; `parkfile.py -s <file>...` prints a file's stamps beside
  the staff's newest `mTimeHired`; `q333/mk.py` made the item's files). 692 ticks after the
  stamp is day 30 and 691 is day 29. The scene is `q333/scene.py` (the stock park paused on tick 915, `fire 26`,
  `buy 1101 41 22 0`, `buy 1101 70 40 0`, `savepark`): with the stamp of tick 237 both games say sample 20 in
  the hard hat on tick 929 (774 and every 155 on) and leave tag 9, whose bar's first button builds the hire
  screen on its mechanics' tab. The same park says row 113's line on tick 996 and row 259's on 1053 in both
  games. In the original `thingwords.py %PID% SECS 5:0x21c38:Q:lasthire` reads the stamp; `hire <candidate> <x>
  <y>` in this game's console stamps it (`candidates` lists the pool), and the log says so.
- **A poor selection of shops, and the advisor's line about it** (Q336): row 71 wants two shops, under 25 per
  cent of the kinds of shop on offer standing, and thirty days since the newest build. **The shipped park
  offers three kinds, so it cannot score there**: mark two more researched in the file first
  (`original/research.py <in> <out> 1206`, then `1212`: five on offer, one kind is 20; both games keep a byte
  written 1 for a shop whose research costs money). `q336/scene.py <out> <bin> base1 1203` is the stock park
  paused on tick 917 with `buy 1203 41 22 0` (a shop on that cell stands open); `q336/mk.py` marks the shops and
  writes the stamp of tick 271 (day 30 on the row's turn, tick 963; 808 and every 155) and of 272 (day 29).
  Both games say sample 205 with no hat on tick 963 and leave tag 73, whose bar's first button builds the buy
  screen on its shops tab ("Buy Shop"). **In the original the frame of him is taken with `GRAB=1`**
  (`a-g10.png`): `run.sh`'s own `told` shot comes after `loadfile.sh` returns, twenty seconds on, when a
  fourteen-second line is over. **A silent file's proof of its tick is the next load's log**, whose first lines
  are the park before it (`c`'s park is read at tick 1136 in `d-load.log`).
- **A poor selection of rides, and the advisor's line about it** (Q335): row 60 wants three rides, under 30 per
  cent of the kinds on offer standing (the shipped park offers four: one kind is 25, two are 50) and thirty days
  since the newest build, the analyser's `mLastObjectBuilt` at thing 5's record's `+73516`
  (`parkfile.thing_set( src, dst, 5, 73516, '<q', parkfile.filetime( tick ) )`; a purchase stamps it with now, so
  it is written back after the scene is saved). `q335/scene.py <out> <bin> base1 2` is the stock park paused on
  tick 916 with `buy 1100 41 22 0` and `buy 1100 70 40 0`; `q335/mk.py` writes the stamp of tick 265 (day 30 on
  the row's turn, tick 957; 802 and every 155) and of 266 (day 29), and `offer3`, the Aztec Mayhem not
  researched (`research.py ... 1104 0`: three on offer, 33 per cent, silent). Both games say sample 173 with no
  hat on tick 957 and leave tag 62, whose bar's first button builds the buy screen on its rides tab. **Load a
  second file into the original only after closing the screen a click opened, or stop it first**: with the buy
  screen up the list's click loads nothing, and the next run's pollers then log the wrong park. **A log of
  several loads is read a stretch at a time**, cut wherever `mGameTick` falls back.
- **Broken rides and no mechanic, and the advisor's line about it** (Q332): **a ride bought and never opened
  does not break** when `scriptvar <thing> VAR_BREAKSTAT 1` is written, so break open ones: the stock Bounce
  (13), a Hot Pot (`parkscene.hot_pot()`) and an Aztec Mayhem with its queue joined and guests sent. Each break
  with no mechanic posts `0x44` (one line, the others inside its cooldown), and three shut rides of three have
  row 259 speak on its turn (898 and every 155): to keep both out of the way, buy two Crazy Apes before tick
  895 so row 259 is said then, sell them, break the rides past tick 1086 and save a few ticks before row 6's
  turn on 1238 (928 and every 155). With three broken he says sample 16 in the hard hat and leaves tag 8, whose
  bar's first button builds the hire screen on its mechanics' tab; with two, nothing. The original, given the
  file, says it on tick 1238 too, then row 124's line on tick 1317 and row 7's from tick 1394 (`q332/scene.py
  <out> <bin> <name> <N>`, `q332/confirm.py`, `q332/orig/run.sh <file>`). **`loadfile.sh` cannot load a second
  file into an original left with a park screen open** (its clicks open no menu and the log holds only the old
  park's first lines): close the screen, or `loadfile.sh stop` and start fresh.
- **A park with no ride, and the advisor's line about it:** the same pause with `sell 13` alone: row 62 is
  polled on tick 959 (804 and every 155 on) and he says sample 179 in the hard hat and leaves tag 64, whose
  bar's first button opens the buy screen on its rides tab (`windows` names the buy screen's tab; open it with
  `control 0x26`'s middle and click `control 0x1fd`'s for the shops tab first, so the button has a tab to
  change). The score grows by 3 a day, so sell later for another number (both sold on tick 1074: 94 in the
  original's slot on tick 1114). `advisor` scores each topic on the tick it is ASKED, not the tick it was said.
  **The original's words for a tag are the american folder's**, which can differ from the English one's that
  this game reads (string 67: "now is" and "now's"): take the words to predict from `strdump` on the folder
  the game under test reads. **Waiting on `loadfile.sh`'s log for a record**, leave out the first start's
  lines, which hold the same record: `grep -v "^ *0\.00 s" log | grep -q ...` (`q322/confirm.py`,
  `q322/scene.py <out> <bin> <name> <tick> <things>`, `q322/orig/run.sh <file>`; Q322).
- **The camera's pan held against an edge in the original** (Q625, `q625/orig/run.sh`): the stock park by `loadfile.sh`
  off-screen, `setcam.py PID X,Z 110 R off` 40 units inside an edge, `xdotool keydown KP_4` (KP_6, KP_2, KP_8) for 4 s
  with `campoi.py PID 7 0.03` beside it. **A keypad key's release is often lost** under Wine (two of the seven seen here): its
  scroll term runs on at full speed into the next round, which reads as the point not staying where `setcam.py` put it.
  Read the term (`campoi.py PID 0`) after each release and press and release the key again until it is nought.
  Releasing the key under both its names (`KP_4` and the no-NumLock `KP_Left`) did not prevent it (Q625 round b: KP_8's release
  was still lost), which is how Q625's later rounds were polluted; since Q667 `q625/orig/run.sh` puts the point back inside and reads the term after each
  release and releases again until it is nought (past the edge a stuck key's term reads nought: the bound drops the push),
  and `TAG=c OUT=<dir>` holds all four edges from 40 inside (`q667/orig`).
- **The original's camera keys held, one at a time** (Q631, `q631/orig/run.sh`): as Q625's above, `setcam.py PID 480,400
  ZOOM 0 off` twice 1.5 s apart (the second catches a coast the first left), then `xdotool keydown Up` (Down, Left,
  Right, `w`, `d`) for HOLD s beside `campoi.py PID HOLD+2 0.03`; `camrate.py` reads the rate. Read the options word
  first (`[0x0078d912]`): set, as the reference player's is, an arrow turns a quarter once a press, not 0.002 a ms.
  In this game the log's `Camera: pan by key forward F right R` and `let go after T s` lines carry the point at each end
  and the held time (`q631/confirm.py`, `pancheck`): a screenshot taken mid-hold keeps the key down about 0.2 s longer.
- **Into first person, turned, and out by C, in both games** (Q626, `q626/orig/run.sh`, `q626/confirm2.py`): in the
  original `gmove.py align`, then the pointer glided to (512, 384), THEN `setcam.py PID 450,165 110 180 off` (set straight
  after the align, the point slid to (494.6, 118.6) and the pick's click fell on grass: round a), the gadget's pick and
  the path at (290, 395) (Q496's), the pointer at (1000, 384) for 1.5 s turns about 98 degrees, `xdotool` key `c` leaves;
  `campoi.py` beside it reads the point, both rotations and the saved copy. Here `camera X Y 110 -ROT`, C by XTEST,
  `walk -1 0 60` (back along the path), the pointer at (1260, 360) to turn, C; the log's `First person: entered` and
  `left` lines. Ours stands the viewer at the orbit's point (Q25), so a point on a solid cell, as (450, 165) is, walks
  5 units and stops (round ours-1): use the path's (481.897, 166.285) when the walk matters.
- **The park map in both games** (Q624, `q624/orig/run.sh`, `runb.sh`, `q624/confirm.py`): the stock park (`q325/stock.TPWS`
  in the original by `loadfile.sh`, `setcam.py PID 480,265 130 180 off` after `gmove.py align`), Space opens the map in
  both. The original (1024 x 768 frame): b_plus `(123,677)`, the right arrow `(118,618)`, the up arrow `(79,578)` (inside
  its wedge; the arrows take the pointer by seven-point regions), OK `(991,690)`; `mapview.py PID` reads the view, and a
  held arrow is `xdotool mousedown`, a wait, `mouseup` with `mapview.py PID 3 0.25` running beside it. A fresh process starts
  at zoom 1; the zoom is kept between openings within one. Here `map` is the census (`map zoom Z` sets the kept zoom), and
  **resume after a `step`**: the console's step holds the frame clock, under which a held arrow moves nothing. OK has no
  id to ask `control` for: work it out from its rectangle (1941,1339)-(2024,1422), centred with the frame.
  **The overlay's switches and pickers** (Q664, `q664/orig/run.sh`, a fresh process so the words start at 1, 1, 1): at
  1024 x 768 the switches are at y 730, rides x 58, sideshows 117, shops 177, miscellaneous 237, tracks 296, visitors 720;
  the metrics satisfaction 789, happiness 848, excitement 908; the staff portraits at x 967, handyman y 247, mechanic 321,
  entertainer 395, guard 468. Park the pointer on the text panel (512,740) between clicks so no record is hovered;
  `mapgrid.py PID` after each click.
  **The hover and its click** (Q665, `q665/orig/run.sh`, `runc.sh`): glide onto a point from `maptarget.py`, twice
  (a move message must land there), then `mapgrid.py PID hover` (the record, the word and the label's text) and
  `mapptr.py PID` (the cell the pointer is on). A dot within 5 game pixels wins over the cell, so aim at a cell at
  least 6 from every shown dot (the stock park's Belly Bounce cell (52,26), 15 off). A click on a hovered thing opens
  its window; close the ride window by its tick (858,426; measured: the map reopened), the visitor window by its own, at about (815,380) in a 1024 x 768 frame (read from `c-c2-later.png`, not tried), never Escape, which raises the game menu where no
  window is up. The records exist only while the map is open: a dump after a failed reopening reads 0.
  **The map's advisor line** (Q666, `q666/orig/run.sh`, `q666/ours.py`): `advisor.py PID 120 0x130` as `loadfile.sh`'s
  POLL prints a line only on a change, so the slot's taking and emptying lines carry the tick the park stood at; here
  `sweeps` before and after, `advisor` for `slot=304`, and the log's `post`/`withdraw message=304` lines.
- **Each text against its box** (Q644): `OPENTPW_TEXT_CENSUS=1` logs every text as it is laid out, `UI text: "<text>" font
  F, W x H in a box BW x BH, x S` and `OVER` where it passes its box, in any screen; `lib/screentext.py` reads it for the
  buy and hire screens' every tab. The original's own: `q644/orig/run.sh` (the two screens by Q636's route, `FILE=`).
- **A park screen's title in both games** (Q676): here `control <id>` ends `lettered font F in (r, g, b) with|without the
  edge` for any control holding text (an edit box aside), and `q676/ours.py <out> <bin> <ROUND>` opens all-visitors, all-items and all-staff by
  V, O and E at 800 x 600; the original's by V, A and S, `q676/orig/run.sh`, with `uitree.py PID 0x007c24c8` (the open
  screen's tree: a `UI_SetTitle` title's rect is the stream's widened, and `0x007c2118` stands in its "text" column).
  `rectcolour.py` on the title's rect gives the yellow and white shares; a bare rect beside it reads 0 and 0.
- **The buy screen opened and closed by real clicks, in both games, and its advisor line:** the gadget's buy
  button is layout point (229, 1181) and the screen's exit button (1930, 859). In the original (1024 x 768) they
  are `gmove.py 114 590` and `965 430`. In OpenTPW the gadget keeps to the window's left, but **a park screen is
  centred in a window wider than 4:3**: scale the layout by the window's height over 1536, then add half of the
  window's width less 2048 times that scale to a screen's x (at 1280 x 720 the exit button is `click 1065 403`,
  not 905). The stock park says the screen's own line (sample 380, about 19 s) a tick after its first opening;
  close inside it and he is cut off, and after three cuts he is asked no more (`q328/confirm.py a|b|off|saved`,
  `q328/orig/run.sh <file> a|b|c`; Q328).
- **The buy list's help rows in both games** (Q495): a file with the bank written (`parkfile.thing_set( src, dst, 8,
  20, '<i', 1500 )`: the stock park's economy is thing 8, `mBalance` at its record's 20) makes some rows dear. In the
  original (1024 x 768, `q495/orig/run.sh`): Buy (113, 590), the tabs at y 123 (sideshows 726, shops 786, features 845),
  the Name heading (621, 184), off the list (300, 200); a row's y by moving down x 620 from y 205 and reading
  `helpbar.py`'s `buy item` (rows about 20 apart from 220). In this game (`q495/confirm.py`) the same layout points
  through the centring above, a row's slot from the `Buy list: tab N, ... rows:` log line and the list's `rows of N
  units`; `pointer` reads the bar and `footprint` the poster's words. The text is in the frame: crop the bar.
- **A golden-ticket ride bought and put down, in this game** (Q709, `q709/confirm-b.py` on `lib/freshpark.py` with a
  player's `gms.dat` copied in): the screen's tick logs `Buy list: cells "<name>" "<price>" (R,G,B), ...`, what each
  slot letters and in which colour; `tickets` reads `spent N left N rides [ids]`. A fresh park opens on its gate,
  where every click meets the drive: `camera 565 625` and `step 10` put cell (56,62), open park land, mid-window, and a
  click there put Jurassic Tours down at (56,61) before Q633; a click now centres the picture on the pointer's cell
  (`ParkBuilding.AnchorUnder`), so its anchor is half its width and depth back from the cell clicked. `savepark` writes the player's `gms.dat` first, though a fresh park's
  own file is refused (`SAVE_PARK_WITH_NO_FILE`); `lib/gmsfile.py` reads it back.
- **The hire screen reached by real clicks, in both games, and its advisor line:** the buy screen's button to
  hire is layout point (1917, 634) and the hire screen's exit button (1930, 859), both a park screen's (centred,
  the recipe above); in the original `gmove.py 958 317` and `965 430`. The same point on the hire screen is its
  button back to buy, so know which screen is up before clicking it. **The gadget's button opens whichever of
  buy and hire was last left on, in both games** (Q330; in the original a global of the running program, kept
  over a load from the list and over Restart Park: stop the original between runs, or leave it on buy). The
  stock file holds the hire line cut off once, so two cuts silence it
  (`q329/confirm.py a|b|off|saved`, `q329/orig/run.sh <file> a|b|c`; Q329).
- **The advisor in a chosen costume, in both games** (Q621, `q621/orig/run.sh`, `q621/ours.py`): the stock park's
  buy screen says its own line bare-headed; closed inside the line and opened again it says it again, up to three
  cuts. The original: start `sayas.sh 18,44,41` after the first line, then buy, buy, and the buy screen's hire button:
  the hard hat (18), the research hat (44), the theme's fly in its helmet (41, slot 1); `advlight.py` beside each.
  This game's `sayas` takes only the gesture-row and ticket rows (18 the hard hat, 434 the theme's fly; 44 and 41 are
  refused, "give a response of the table"), so its research hat comes from `q421/all4.TPWS` loaded from the list, row
  16's own line on its turn. Wait for the line in the log from a mark taken before the click: the click's own
  `reply` can take the line. `q621/parts.py` measures each part's colour in the frames.
- **Restart Park and Exit To Lobby by real clicks, in both games:** in the original Escape, then the menu's
  Restart Park row at `gmove.py 510 228` and the question's tick at `708 283` (25 s to the restarted park), or
  Exit To Lobby at `510 522` at the install's 800 x 600 (`510 573` there is Quit Game, whose question comes up;
  Q544), no question; 30 s to the lobby, then `660 58` clicked twice enters the park as `loadfile.sh` does.
  **Exit To Lobby writes `autosave.TPWS` into the reference player's jungle folder and
  rewrites `gms.dat` with the same bytes**: `loadfile.sh stop` then says the sum changed; take the autosave out
  (it is the newest file there, so the next start would enter the park on it) and the sum is the one before.
  `q544/orig/run.sh <tag>` does the whole round with `tick.py` and takes the autosave out (Q544).
  **Entering a park from the lobby writes `restart.INTS` where the player's jungle folder has none, in both games**
  (Q248), and Restart Park reads it: the reference player's and parkrun's private player's are there already (755),
  so a run that needs it written moves it aside first and puts it back with its time (`cp -p`), or the sum changes.
  `loadfile.sh` enters from the lobby when it starts the original, so its first load writes one where none is.
  `q248/orig/run.sh <tag>` and `q248/confirm.py <out> <bin> [control]` do the round: entered, Exit To Lobby, entered
  again, Restart Park (the tick falls to 755), the file taken out and Restart Park again (no fall).
  In OpenTPW `menu`, a click on `control 3`'s middle, then on `control 9873c9`'s, the question's tick (Return
  through XTEST does not answer it); `lobby` and `park jungle` are the console's way out and in
  (`q330/confirm.py fix|restart`, `q330/orig/run.sh <file> r|l`; Q330).
- **The count of unbuilt polled rows is exact, so predict it exactly:** under a `pause` on tick T the stock
  park's `PARK_ADVISOR_POLLED_ROW_NOT_BUILT` is T - 755 less the turns of built rows by then, and a built row
  comes round every 155 ticks with no log line where its answer has not changed (282 on 760 and 915; 1 on 768
  and 923; 2 on 769 and 924; 62 on 804 and 959; 113 on 841 and 996; 185 on 877 and 1032; 259 to 262 on 898 to 901
  and 1053 to 1056; 263 on 902 and 1057). **List a new row's turn BEFORE the file's last one too** (996 less 155 is 841, past the
  file's tick 755: left out of Q324's first prediction).
  `q320/probe.py` reads tick against count at a list of pauses (Q320).
- **An open park with most of its rides shut:** under a `pause` on a tick from 893 to 896, `buy 1101 41 22 0` (a
  Crazy Ape) and `buy 1104 57 23` (an Aztec Mayhem), their queues left unjoined: three rides, two shut. Saved
  there, the original starts a second before the advisor's row 259 on tick 898, before any of its other rows has
  seen the new rides; its row 113 then speaks of the unjoined queue on tick 996. **A line said a second after a
  load is photographed by a grab loop** started from the Load Park list's frame, every 1.2 s for twenty frames
  (`q323/orig/run.sh`; frames 3 to 9 hold the line), since `loadfile.sh`'s own come 6 s and 15 s on (Q323).
- **A queue not joined to a path, and the advisor's line about it:** the same pause with `buy 1104 57 23` alone
  (thing 43, its queue left unjoined): row 113 is polled on tick 996 (841 and every 155 on) and he says sample
  312 with no hat and leaves tag 135, whose bar's first button, the camera's, does nothing (the tag is about no
  thing). `tool queue 43` under the pause keeps the row at nought until `tool off`. The original, given the file,
  says it on tick 996 too; `tpwmem.py --pid %PID% watch 008186ac <secs>` in the `POLL` reads the builder's
  object beside `advisor.py ... 0x71 poll` and `tags.py` (`q324/confirm.py fix|control|joined|armed`,
  `q324/orig/run.sh <file>`; Q324).
- **A shut staff room, and the advisor's line about it:** no screen of either game shuts one, so the scene is a
  file: `parkfile.thing_set( Easymode.TPWI, room-shut.TPWS, 20, 214, '<i', 0 )` (thing 20 is the Staff Room,
  214 an object's `mCanLoad`), copied into the private jungle folder and loaded with `load( 1 )`. Row 263 is
  polled on tick 902 (and every 155 on) and he plays sample 638, the bank's `z_error.mp2`, with no hat and no
  tag, for 3,998 ms; the original, given the file, says it on tick 902 too. **To find a short sound in the
  game's own mix, open the window four ticks before the tick it is owed on**: `tick()` is a console reply and
  comes back up to a second late. **A control that must stay silent in the original needs a tick stamp past
  the turn**: `thingwords.py %PID% <secs> 29:0x8:H 35:0x8:H` (two guests' places) beside `advisor.py` stamps
  the log as they walk (`q325/confirm.py fix|control|open`, `q325/orig/run.sh <file>` with `EXTRA=`; Q325).
- **Open a bought ride:** `buy <item> <x> <y> 0` answers the thing's id and its queue node's cell; then
  `tool queue <thing>`, `worldclick <the node's cell>`, `worldclick <a path cell beside it>`, `tool off`. For a Hot
  Pot on (41,23): `buy 1140 41 23 0`, node (43,22), path (43,21); four boats are out six seconds on (Q257r).
- **Fill a ride:** `admit <x> <y>` on a path cell answers a new guest's id, `send <guest> <thing>` sends them; poll
  the ride's census (`bumpers`, `rides`) until it counts them (Q179d, Q257r).
- **The rest of Lost Kingdom's rides, shops and sideshows with guests** (Q584, `q580/confirm.py`): Tom Tom Twister
  `buy 1113 40 22 0`, node (42,21) joined at (43,21) (on (41,22) its node lands on the path and no queue joins); Mayan
  Spinner `buy 1108 57 23 0`, node (58,22) joined at (56,22); the Burger `1207` or the Fries `1212` on (41,30); the Giant
  Puzzle `1307` on (40,22) (with the Twister on (41,22) its node takes (43,21), and a guest admitted there has no route to anything). `send` refuses a guest still in their
  arrival's spot animation: send again each second for up to 12 s. A shop's visit is the log's `paid P at object N`
  then `Happiness changed by X since using object N` (X is three times the change in the guest's happiness since they joined, each as its low byte,
  `ParkRideOperation.cs` / `0x004fda47`; the ride's own part is the `excitement` line before it, a shop's the
  `ingredient` line). A spot that may not reach the path: buy, send one guest, `sell` on "no route", next spot.
- **The four Full Simulation ones with guests** (Q580, `q580fs/confirm.py <out> <bin> S|K|G|A`, `lib/fsplayer.py`): each
  fits on (40,22) in the stock park entered as Full Simulation, thing 43; Sun God `buy 1106 40 22 0`, node (42,21)
  joined at (43,21); the Steak Restaurant `1208`, the Gift Shop `1211`, the Arcade `1309`; four guests admitted on
  (43,21) and sent. On (57,23) the Steak and the Gift Shop fit and no guest reaches them, and turned they do not fit.
  The original given the saved scene: `q580fs/orig/run.sh <S|K|G|A>`.
- **The research screen in both games** (Q590, `q705/confirm.py <out> <bin>`, `q705/fs1.TPWS`): a Full Simulation
  park with a researcher is q327's `FSFRESH.TPWS` (the original's own fresh park, researcher thing 28; his grade is the
  record's `+398`, `parkfile.thing_set( src, dst, 28, 398, '<i', 1 )` for ability 3). Here: the private player put to
  Full Simulation (`lib/fsplayer.py`) and the file made the newest jungle file, so `enter()` loads it; R opens the screen,
  `researchscreen` is its census, its close button is layout (1930, 885) and a panel's right arrow (1054, 233 + the
  panel's offset) through `layout_point( "d2cff", (186, 30, 2018, 1032), ... )`. The original: loaded by hand as the `fsload.sh` row says;
  R, the close button at (965, 442) of 1024 x 768, `labread.py PID 0` for the work load, efforts and outputs. Its hire
  in `freshfs.sh`'s park would not take: every cell under the hand showed the red square (`q705/orig/a`).
- **An interface animation's drawn rhythm in both games** (Q640, `q640/ours.py <out> <bin>`, `q640/orig/run.sh OUTDIR`):
  the scene of the row above, opened by R; the control's screen rectangle from `layout_point` and the window's origin
  (`win.translate_coords`) here, and from its 1024 x 768 point (x 320 + 1.875 x, 1.875 y) on the real screen there; then
  `rectfilm.py` on it, 2 px or more inside its edge, beside the game's own counter (`researchscreen`'s passes here,
  `respump.py PID 16` there, started half a second before). Both games' gaps fall on the 144 Hz desktop's 6.9 ms steps.
  A scroll drawn smoothly (Q677, `q677/ours.py <out> <bin> <ROUND>`): `rectfilm.py --min 0`, whose every change counts,
  then `scrollphase.py` on the film at the quad's width / 1.998 (36.84 px here, 73.57 on the original's real screen);
  `pause` and 3 s more of film is the control, nothing changing.
- **A model's drawn rhythm in both games** (Q642, `q642/ours.py <out> <bin> <ROUND> [X,Y,W,H]`, `q642/orig/run.sh OUTDIR
  [RECT]`): the stock park, one camera by `bothcam.py`'s numbers (530,250 70 180 puts the Bouncy Dino mid-frame),
  `rectfilm.py --min 1.5` on a moving part, and `pause` for the control here. With no rectangle `ours.py` takes two shots
  0.1 s apart to choose one clear of walking guests. The original at `ORIG_FPS=60`, or its 30 fps cap hides a per-frame
  draw behind the 32 ticks. Keep the rectangle small, about 140 x 110 screen pixels on the original: every read of a
  picture that changes each frame pays rectfilm's diff, and 420 x 340 read 28 times a second (`q642/orig/O`).
  `q642/orig/run.sh` takes that 420 x 340 as its default rectangle: always pass one (round O2's `1450,580,140,110`).
  `q642/ours.py` and `q642/orig/run.sh` are the item's own drivers, not in `lib/`: copy the camera and park-entering
  steps from them, and move them to `lib/` if a third item wants them.
- **A clip's sweep moment, and the bus filmed driving in** (Q675, `q675/confirm.py <out> <bin> <ROUND>`): `rides` prints
  `now` and `last tick at` in its header and each channel's `time` (its last advance): a clip swept at the last tick's
  instant reads a `time` that divides by 31, one swept at the frame's own moment mostly does not; a HELD channel's
  `time` is its start less the clip's length, never an advance, so leave those lines out. `bumpers` prints each boat's
  `clip start`, `time` and the header's `now`. The bus: `camera 475 60 90 0`, wait for the log's `the bus summoned`
  (about 126 s after the stock park's entry, `mGameTick` 1264: that count is the thing sweep's, about 4 a second, not
  the 32 ticks; near 1090 in the runs where a leaver summons it first, on any build), then 3.5 s, then `rectfilm.py --min 0.5` on window 1040,240 120x30 for 6 s: the roof crosses it
  above the flags (which wave every frame) before the bus stops by x 960. A Hot Pot's boats in view: `camera 420 245
  90 180` after `hot_pot()` (`buy 1140 41 23 0`); the smoke recipe's 435 320 110 180 frames the smoke, not the pot.
- **A golden ticket won in both games** (Q591, `q706/`): a copy of Alexah's Full Simulation profile with tickets taken
  out, `gmsfile.put( copy, tickets={'jungle': [0,0,1,0,0,0]} )` (`q706/gms-L01off.dat`), so her jungle `New Save.TPWS`
  (tick 19004, 759 visitors to date, some 280 inside) wins Locals 0 and 1 again on tick 19100. Here: `q706/confirm.py <out>
  <bin> L` (`fs_profile`, `enter()`, `pause`, the file loaded from the list, run to 19101 and to 19201, the `tickets`
  census, `savepark` and the private `gms.dat` read back; `A` Alexah's own profile, `I` the Instant Action player, `P` back to the lobby after the award and each island's panel shot). The
  original: `GMS=<copy> POLL='python3 -u progress.py %PID% 120' GRABS=45 fsload.sh <the file> <out>`; it ran past 19100
  under the fresh-boot clock (`q706/orig/O`), and its gadget's corner shows the counts (`g-05`, `g-40`).
- **A golden ticket's line said in both games** (Q595, `q707/`): the same profile and file as the row above;
  `q707/confirm.py <out> <bin> L` stops at 19090 (a stop at 19097 overshot past the award), loads the q707 row (row 2:
  the private folder's own `autosave` is row 1) and shoots the line's clips at set offsets from its `Advisor: sample 446
  in` line; `F` with `q707/gms-none.dat` (every ticket and record flag cleared) gives the first ticket's two lines.
  `q707/p707t/confirm.py <out> <bin>` reads the two lines' tags (`tags`, one line now: a text's break is `\n`), clicks
  tag 102 to the bar (`tag_click`) and shoots both; the original's tags, `POLL` `tags.py` beside `advisorline.py`
  (`q707/p707t/orig/O/run.sh`). `q707/p600/confirm.py <out> <bin>` shoots the glints through the same line, and with `PAUSEAT=<s>` pauses there and
  reads each effect-44 emitter's particles by `particles <slot>` (an on-screen emitter's offsets and velocities). The
  original: `GMS=<copy> POLL=<armed advisor.py + advisorline.py> fsload.sh`, filmed with `record.sh` from the first POLL (started before `fsload.sh`, it finds no display `:77` and films nothing, Q597).
- **A made file's advisor row in both games** (Q743, `q743/`): the original by `q743/orig/run.sh` (`fsload.sh`, `GMS=q707/gms-none.dat`,
  the readers armed on the load's `POLL`: `advisor.py <rows> poll`, `advisorline.py`, `thingwords.py` on the object's lifetime
  counts `+0x1a0`/`+0x1a4`, `loadframes.py %PID% 150 - 0 0 $D/f 1.0` with `JUMP=1`); this game by `q743/confirm.py <out> <bin>
  [UNTIL]`, free-running from the load (`LOADROW=1` on the desk, whose private folder lists one park; lane B's lists two),
  `SHOTS=<samples>` to frame only those lines. A line posted and said on one tick is never seen in a slot: its record's
  tick is the evidence, not its score.
- **The golden ticket's line at a chosen window size, in both games** (Q639, `q639/confirm.py <out> <bin> WxH`, `q639/orig/run1280.sh`):
  q707's scene (above) with the console's `size` before the line, `LOADROW=1` when the private folder holds no
  `autosave`; ticketbox.py on the shots and the ticket's middle against the window's. `xdotool windowstate` does not maximize
  the game under the desk's KWin; a maximized window's inside is `size 2560 1366`, which KWin trims to 2552 x 1355 (the
  work area is 2560 x 1396). The original at its 5:4 mode: `resolution.sh 1280`, its own Xvfb `:77` at 1280 x 1024 started
  first (`original/xvfb/root/usr/bin/Xvfb`; at Res 5 on the stock 1024 x 768 display it dies with black frames), and every
  1024 x 768 point of `freshfs.sh` and `fsload.sh` scaled by 1.25 x 4/3, as its interface stretches; `resolution.sh 800` after.
- **A park's first golden ticket, in both games** (Q629, `q629/`): the stock park played as Full Simulation by a
  profile with no ticket (`q707/gms-none.dat`) wins Local 0 on the first test past 100 visitors, about tick 7400 (some 28 minutes), with
  nothing done. Here: `q629/stock.py <out> <bin> [limit s]` (`fs_profile`, `enter()`, run to the first `Won a` line,
  the lines shot as samples 29x, 589 and 598 start, `tags` and `tickets`). The original: `GMS=<profile> freshfs.sh make
  <out>`, `Easymode.TPWI` copied as `Stock.TPWS` into `save/users/2fsfresh/jungle`, Escape, Load (510, 50), the row
  (400, 123), then `ticketmeasures.py`, `progress.py` and `tags.py` on `original.sh pid`; `freshfs.sh stop` removes the
  file with the player. **A fresh park's gate starts shut in both games** and a bare park draws about four visitors a
  thousand ticks: open it as a player, G then the door and OK (ours: `control 0x4f3b1`, OK at the door's rect plus
  (282, 78) of 205 x 169 layout units, `q629/confirm.py`; the original's at 1024 x 768: the door (726, 381), "Left-click
  to open the park (Ctrl-O)", OK (815, 378)).
- **Several `object.py` readers in one `loadfile.sh` POLL** (Q584, `q580/orig/run.sh`): their lines name no thing, so
  tag each, `python3 -u object.py %PID% SECS N | sed -u "s/^/[N] /"`.
- **A Crazy Ape running in both games** (Q558): `q558/confirm.py <out> <bin>` pauses the stock park, `buy 1101 41 22 0`
  (thing 43, its queue node (42,21)), `tool queue 43`, `worldclick 42 21`, `worldclick 43 21`, `tool off`, resumes, admits
  six guests on (43,21) and sends them to it, waits for the first loop's `KILLOBJ 10` and saves the park after it
  (`run4/q558ape.TPWS`). The original loads that file with `loadfile.sh` and plays the Ape's loops on (`q558/orig/run2.sh`);
  on the real screen `q558/orig/screen.sh` enters it with `screenpark.sh`'s clicks and puts the camera on it with
  `setcam.py PID 430,240 90 0 off`. Alexah's Full Simulation park, which holds a Crazy Ape too, froze in the original
  (warning 13).
- **A held chain started fresh in both games** (Q559): the stock park's gates' chain 177 is no use, as the original puts
  it back from the file and ours starts none (Q560). `q559/confirm.py <out> <bin>` pauses the stock park, `buy 1417 45 20 0`
  (a Loudspeaker, whose script opens on `ADDOBJ 4 -1 177 10`) and saves at once (`q559spk.TPWS`, before its script turns),
  then resumes and reads the log's `held chain 177, child` lines. The original enters that file on the real screen with
  `OUT=... FILE=<it> CAM=455,205 BRK=0x006bdb1d,0x006c3aba original/screenchains.sh` (`q559/orig/`, run g): the first
  address hits once for the fresh chain (its key, flags, own variation), the second after each child's draw. Set the
  breakpoints once the lobby is up, as run g did: whether one set as the game starts reaches the sound thread is not
  tested (run d's `0x006bdb1d` could not hit, no fresh chain taking the first-child branch there), and `brk.sh`
  attached to a park already running hits at once (run e).
- **The bus's engine in both games** (Q561): the stock park, the camera on the bus stop. The original:
  `OUT=... CAM=475,60 SECS=260 EXTRA="musicvoices.py 6" LATE="mixfreq.py" original/screenchains.sh` (`q561/orig/`); the bus
  is summoned about 66 s into the park (its voice came at +181 s, +182 s and +193 s of the pid in three runs, the park
  clock fast), so `SHOTS="200 203 206"` or later catches it at its first stop. Ours: `q561/confirm.py <out> <bin>` reads
  the log's `music 6` lines and `scriptsounds`; the bus is summoned on `mGameTick` about 1264.
- **A voice taken out of range and back, in both games** (Q563): the original under `screenchains.sh` with `KEEP=1`
  (so it stays in the park), a watcher on `<TAG>-musicvoices.log` that waits for the bus voice's first line with a
  channel, then `setcam.py PID 200,200 90 0 off` and after a while `setcam.py PID 475,60 90 0 off`, a frame each way
  (`q563/orig/run.sh`; `musicvoices.py` marks it `0x802`, `ch 0x0`). Ours: `camera 200 200 90 0` and back once the log
  says `in range again`, the log's `out of range at` / `in range again` lines and `scriptsounds`' `away` (`q563/confirm.py`;
  take the log's mark before sending the camera command, as the line can come inside its reply). **Build nothing while
  `screenchains.sh` starts the original**: its lobby clicks are on fixed sleeps, and a build beside it left the game on
  the player menu (run a).
- **A ride's screams taken out of range and back, in both games** (Q564): the stock park's Belly Bounce screams in
  short chains (kids 71 to 73, 1 to 3 s each, about one a second near). The original: `screenchains.sh` with `KEEP=1
  CAM=525,250`, then 40 s there, `setcam.py PID 200,200 90 0 off` for 40 s and back, `chains.py`'s log split by its line
  count at each move (`q564/orig/run.sh`; the listener stands 28 from the ride). Ours: `camera 525 250 90 0`, `camera 200
  200 90 0` and back, the log's `screaming, band`, `scream N, effect`, `out of range at` and `in range again` lines and
  `rides`' scream state (`q564/confirm.py`).
- **A script's one-shots taken out of range and back, in both games** (Q565): the Jungle Spray (`Junspray.RSE`'s `EVENT`s
  159 and 174 to 176 at (525, 315), 90 range, and 212, a win: once in `q566`'s 60 s, none in its 60 and 150 s controls) sounds every few seconds; the toilets' kids 48 to 53 only when a guest visits,
  and some 40 s runs have none. The original: `run.sh` with `CX=525 CZ=300` (`screenchains.sh`, `EXTRA="oneshots.py"`),
  40 s there, (200, 200), back; `oneshots-phases.py` splits the log at the line marks. Ours: `WARM=40` before the near
  phase: the Spray sounds only while a guest plays it, and the first boards it about 53 s after the entry, after the
  Belly Bounce's first ride; `camera 525 300 90 0`, the log's
  `started effect`, `held`, `dropped after` lines and `scriptsounds`' "one-shots dropped" (`q565/confirm.py`). The
  original's `import` grab failed on four of six frames once: retry it (`q565/orig/run.sh`'s `shot`).
- **The happiness gauge at a known value in both games** (Q244): `q244/mkscene.py` writes one happiness into every
  guest of Q415's `noent.TPWS` (`parkfile.thing_set` on `+422`, all model-1 things, so the mean is the same whichever
  guests a game counts); ours loads it from the Load Park list (`q244/confirm.py`: `load()`, the log's `UI: meter 0x1f at
  <units> of 1024, the boundary <p>% up`, `control 1f`, a shot), the original through `loadfile.sh` (`q244/orig/run.sh`,
  its `-3-later` frame); `lib/gaugesplit.py` on each box. The original's clock runs on through its load, so its guests
  drift a point or two lower than ours, paused on 766.
- **A crowd at a happiness, in a file both games load:** `lib/stockscene.py ... 760 "fire 27" "admit 52 26"` x45
  `"admit 52 34"` x45 (thing 27 is the stock entertainer; each command takes about 2.5 s) pauses on 765 with 93 inside,
  then `parkfile.thing_set( ..., guest, 422, '<f', h )` writes each happiness (`q415/mkscene.py`; `parkfile.py -g` counts
  them). **Five of the stock guests on the road walk in by tick 781**, in both games (the original's `inside.py ... 60 30` reads 98 on 773): count 98 inside there. The original says a line before its stills: film it (`record.sh start` in the `POLL`, `q415/orig/run.sh` with `RECORD=`; Q415).
  **Thirsty or hungry, the same way:** the guest record's thirst is `+509` and its hunger `+426` (floats);
  `q530/mkscene.py` writes 100 into every guest inside Q415's `ent.TPWS`, and both games say rows 3 and 4 on ticks 770
  and 771. `loadfile.sh` returns about 16 s after its Load Park click, so a still taken after it misses a line said
  at the load: read the film (`record.sh frames`), and under `set -o pipefail` wait with `grep -c`, never `| grep -q`,
  whose early exit fails the pipe on the SIGPIPE (`q530/orig/run.sh`; Q530).
- **Two crowds from one run, past a hire's 30 days:** a script on `lib/parkrun.py` (`q416/mkscenes.py`) pauses the stock
  park on 1233 (it stops on 1236; 12 inside), gives `fire 28` (the guard), admits 168 and calls `save()`, then admits 32
  more and calls `save()` again: one game, two files, 178 and 210 inside (each `admit` about 2.5 s, so ~9 minutes). The
  newest hire's 30 days come on 1340, so the advisor's turns on 1243 score nought and 1398's are the test (Q416). **`wait_for`
  takes plain words, not a pattern**: a regex there waits out its time and the run judges nothing.
- **A littered park:** `lib/stockscene.py` pauses the stock park (on 768 from 765) with `"fire 25"` (thing 25 is the
  stock handyman), then `parkfile.litter_set( base, out, path_cells[:N] )` writes litter on the first N path cells
  (`q417/mkscenes.sh`): the stock park walks 90 cells, so 12 littered is a share of 13 and 27 of 30. Both games load
  it; the original's handyman sweeps a cell every 15 or 16 ticks, so keep him dismissed or read the share with
  `original/litter.py`. **A filmed run (`RECORD=`) waits 6 s more before its clicks**: past a tag's 30 s life, so film
  one run and click in another (Q417).
- **An Aztec Mayhem with riders:** `buy 1104 57 23`, then the queue: `tool queue <thing>`, `worldclick 58 22`,
  `worldclick 56 22`, `tool off`. Two riders are carried about 40 s on, up to five before the ride starts.
  `camera 585 245 25 90` looks down into its pit, where the riders' heads are; from further off its walls hide
  them. In the original the lid is shut over them while the ride runs, so its frame shows no head (Q257u).
- **A Balloon Shop a guest can reach:** `buy 1209 43 22 0` (the review of 2026-10-06, fix 2).
- **Particles a script starts:** `buy 1203 44 33 0` is a second Drinks Shop, whose script opens on its bubbles
  (effect 58 on node 1); `scriptvar 21 VAR_WORN 1` makes the file's Small Toilet 21 start two (9, then 69).
  `emitters` prints the LOADED FILE's emitters; `particles` the running park's, with how many were drawn.
  `camera 445 335 45 180` shows both Drinks Shops' bubbles, `camera 555 170 25 180` the toilet's stink (Q258).
  In the original's frame the bubbles are a few pixels: crop the shop and enlarge it four times (Q257v).
- **Who starts a particle effect in the original, and which** (Q663, `q663/orig/run.sh`): `emitters.py PID SECS` and
  `brk.sh 0x00521e60,0x00521930 12 10 60` (`Particles_Spawn`, `Particles_SpawnFull`) armed together a few seconds before
  the act, with `BRK_COND='(*(unsigned*)$esp < 0x51ef00 || *(unsigned*)$esp > 0x522300)'` so the particle system's own
  calls take no hit and `BRK_PRINT` printing the four arguments (effect, x, y, z over 1024). The first stack word is
  the caller. Effect 19 at the gate (`0x004fa522`, peeps arriving) is the stock park's steady noise. A Round Fountain
  in `late30.TPWS`: Buy, the features tab, its row (575,261), `setcam.py` back, then the ground at (585,275).
- **The Jungle Spray's jet:** `admit 52 34` four times, each `send <guest> 14`; poll `rides` for a `2:37@`
  record on thing 14 and `pause` at the first: one jet (node 2) is running, for about four seconds (Q257w).
- **A broken Hot Pot with smoking boats:** fill the pot (above), and once `bumpers` reads `state Running` with
  riders seated, `scriptvar <thing> VAR_BREAKSTAT 1`; every boat reads `smoke started`. A break between goes
  smokes no boat (Q257x). `particles` then reads an emitter of effect 2 a boat; `camera 435 320 110 180` shows
  the smoke over the pot, cropped at (560,90)-(730,260) of the 1280 x 720 frame (Q259).
- **The Hot Pot's own smoke** (effect 16, two emitters, started by a break): `broken_pot()`, then
  `camera 435 250 110 0` shows the cloud at its doorway from the side the original's file `hotpot-later2.TPWS`
  looks from (OpenTPW yaw 0 looks toward higher z; yaw 180, Q259's, is the pot's far side), and
  `camera 435 228 35 0` its sign close up. `particles <slot>` prints every particle of one emitter (Q261).
- **The waterfall's spray** (effect 20, the land's own, running from the park's first frame):
  `camera 532 526 25 0` looks at the foot of the fall from the side the water comes down; `camera 532 591 110 180`
  (`parkrun.original_view( 532, 526 )`) saves a view the original's frame shows it in, from the far side (Q262).
- **The mechanic mending a pot:** `broken_pot()` (`lib/parkscene.py`), then wait for the log's `mechanic 26 found
  ride`, `starts repairing` and `finished repairing`: under a minute in all. He stands on (43,23), inside the
  pot's doorway, hidden from yaw 180: `camera 435 235 30 0` looks in at him (Q257z).
- **A placed sound measured in the mix:** `AUDIO_FILE` before `parkrun.py`, `bus music 0`, then **wait 16 s
  after the park is entered**: before Q316 an opening line turned the effects down there (a waterfall read 0.05 to
  0.07 where 0.176 was owed); the stock park now says none, and the wait was not measured again. Take byte marks either side of an 8 s sleep and use `mixmatch.steady`: a 1.5 s
  template scores 0.2 under the park's screams, a 0.3 s piece 1.000 in the gaps. `placed` prints the listener
  and each of the land's sounds with its distance and gain (Q264).
- **A ride that screams, and nothing heard:** the Belly Bounce (thing 13, cell (51,23)) screams inside a minute
  of the stock park's start; `camera 515 235 60 180` puts the listener 34 units off it, and `camera 1100 1100 110
  180` is 750 and more from every sound in the park, where the mix with `bus music 0` is exact nought. `voices`
  prints each ranged voice's place, range, distance and gain; a scream's child lives a second or so, so poll it
  every 0.4 s and gather (Q265).
- **A one-shot measured in the mix:** `AUDIO_FILE`, `bus music 0`, 16 s, then poll `voices` for 40 s between
  two byte marks and `mixmatch.match( window, template, 0.5 )` each sample named: a scream child scores 0.99 to
  1.000 and reads one gain to four places (`q267/confirm.py`; `find( name )` there looks a sample up in
  `KidsHD.sdt` and `xKidsHD.sdt`). The gain is the voice's volume times its distance gain times 0.397 with the
  camera at `515 235 60 180`, the master and the pan together, so compare two builds, not a build with a sum.
  `voices` prints each ranged voice's volume (Q267).
- **A pitched sample found in the mix:** match its template at each pitch (`q297/pitchscan.py`: read at a rate of
  `2 ^ ((p + 1) / 96)`, 1 at pitch 0) over the whole capture. At pitch 0 a hit is 1.000; at another pitch 0.90 to
  0.99, because the template is resampled by a straight line and the mixer steps to the nearest sample, and a miss
  is 0.77 at most: take 0.85 as the bar, not 0.97. A ride's variation repeats its few samples, so hold each
  (sample, pitch) pair of the log against the mix, not each sample once (Q297).
- **Guests put off, each heard:** `admit 52 26` until eight answer an id that divides by eight (only those
  sound), `send <guest> 13` each, wait 30 s, then `scriptvar 13 VAR_BREAKSTAT 1`: the Belly Bounce's queue is put
  out a head a sweep and the log reads a `put off` line each, three to seven a break; the mechanic has it open
  again inside a minute, so send and break again. In the mix the put-off's one sample (`bootout.mp2`) scored 0.95
  at four of six logged pitches and 0.64 and 0.68 at the other two, 0.30 at a pitch not played: take 0.5 as the bar
  (`q298/confirm.py`, Q298).
- **A Hot Pot's engine, read and measured:** `hot_pot(5)` and `in_a_go(120)` (`lib/parkscene.py`), then
  `camera 435 250 60 180`; `voices` names it `Engine.mp2 Effects placed` with its volume, rate and range, and
  `bumpers` prints the lead car's `volume N playing at G`. Its pitch moves each tick, so no template finds it in
  the mix: with `bus music 0` compare the mix's rms over the go's first 12 s between two builds (`q299/confirm.py`,
  Q299).
- **Thunder, read and measured:** `bolt 515 235` aims a bolt and its thunder follows inside three seconds, at the
  bolt's top, which leans up to 50 off: leave nine seconds between bolts or the effect's wait drops one. Its place
  moves the pan, so match its sample in each channel of the capture apart and take the root of the two gains'
  squares: over the volume times `voices`' distance gain that is one number, 0.66, for every thunder
  (`q300/confirm.py`, Q300).
- **The rain alone in the mix:** `bus music 0`, 16 s, `camera 1100 1100 110 180`, then `weather 16`, not 1: a
  quality of 15 or less arms the lightning, and thunder (range 1500) is then the mix. 16 is 360 drops, a level of
  60, reached in 4.5 s. `weather` prints both rain voices (level, variation, volume, what each plays at); the log
  has a line a set and a line a sample. Each `weather N` holds off the park's own next change for some 40 s. The
  two voices are the same sample, a part of a second apart unless the command lands on a weather tick's frame: apart,
  `mixmatch.steady` scores 0.77 and tells no gain and the rms is the two powers' sum; together it scores 1.00 at
  twice a voice's gain. Compare the mix's rms between two builds and allow for both (`q301/confirm.py`, Q301).
- **The gulls and the sea, read and measured:** `camera 400 0 110 180` puts the listener at (400,29,47), inside
  the gulls' rectangle and 8 from the sea's, with every other placed sound out of range; `camera 400 60 110 180`
  is 58 and 68 off, and shows the harbour. `placed` prints each following voice's place, distance, law and gain
  (`gain none` between two samples: poll until it reads `sounding`). The listener is not on whole units, so read a
  distance before predicting its law. A sea sample opens quietly: match its 1.5 s from 3 s in
  (`t[3 * 22050:]`), which scores 1.00 where its first second and a half scored 0.20 under a gull
  (`q302/confirm.py`, Q302).
- **A ride worn out by itself:** enter the stock park and leave it: the Belly Bounce (thing 13) logs `Object 13:
  wear 1.515 with 1 on` every 64 sweeps (16 s), is told it is worn after 53 of them, asks to break down after 66
  (17.5 minutes) and is mended and open two minutes on. `openthing 13` shows its State of repair's bar, `objects`
  prints the number (`q303/confirm.py`, Q303).
- **The crowd's voice alone in the mix:** `hover 50 24` holds the pointer's cell on the Belly Bounce's queue
  whatever the camera does, so with `bus music 0` and `camera 1100 1100 110 180` the mix is the crowd's flat
  voice and nothing else. `voices` names each sample as `<name> Effects flat`; match each at its six pitches
  (the template read at a rate of `2 ^ ((p + 1) / 96)`, 1 at pitch 0): a hit scores 0.97 to 1.000 and its gain
  is the voice's volume times the master's 0.5. `crowd` reads a volume of nought between two samples
  (`q268/confirm.py`, Q268).
- **The orbit camera's eye is not `zoom` from the point it looks at where the ground is high:** the eye is
  lifted by the ground's height under the point and the point stays at height nought. Over the waterfall
  (ground about 12) `camera 530 513 30 180` puts the eye 38.5 off, not 30. Read `placed`'s listener, do not
  work it out (Q264).
- **A harness run needs a python with `Xlib` and `PIL`:** `~/.cache/tpw-harnesses/review-codex/venv/bin/python3`
  where the system's has no `Xlib`. A run that dies before `done()` leaves its Xvfb on `:78`: stop it (Q257w).
- **The camera aims at the ground, so it cannot close in on a Hot Pot's water**, 30 units up: `camera 435 275 45 180`
  and anything nearer shows the pot's wall. Take `camera 435 320 110 180` and crop the pot from the frame (Q257s).
- **`pkill -f 'Xvfb :78'` kills the shell that runs it** (its own command line matches): write
  `pkill -f 'bin/[X]vfb :78'` (Q257x).
- **Take the census last.** `step 2` after a `camera` moves every car and guest two ticks: a census held against a
  file must be taken after the last step before `savepark` (Q257r).
- **A load runs on before `pause` lands**, a dozen ticks: hold a loaded park against the load's own log lines, not
  against a census (Q257r).
- **A run that needs changed options** uses a private game folder, links to the real one and a `save/` of its own
  (`parkrun.py` makes one; the review of 2026-10-06, fix 3).
- **In a test, a boat is launched empty and then filled:** `Launch`, `Board`, `Fill`, as the ride's script does
  (`BUMP 4`, `1`, `12`); `Board` before `Launch` leaves the car not driven (Q257r).

- **`parkrun.gaps( pattern )` needs its pattern** (`gaps( "SOUND_PLACE" )`): called bare it raises, after the
  run's readings are taken.
- **A sound's place is printed as (x, z, height)**, the height last, by the audio log and by `voices`: the Belly
  Bounce's scream reads (525,250,0).
- **`bumpers` prints a line a car**, `car N at (x,z) ...`, each behind `[dbg]`: match `\bcar \d+ at \(`, never from
  the line's start. `parkscene.bumper_line()` is the ride's line only.
- **Read a category's effects one effect a call** (`sfxmap.py <data dir> <map> <effect>`): a shell loop's output
  runs two effects together.

**In the original, off-screen at 1024 x 768** (points for `gmove.py X Y`; `gmove.py align` first, with `GW=1022
GH=766` there, or the pointer is off):

- **It enters a park on the newest file in the player's folder**, so a file copied in before the start is loaded at
  the first frame, and again by the list's click (Q257n). Its track tick is not reset by a load (Q257p).
- **The player screen:** the slot (660,58), clicked twice: the first click is ignored.
- **A list picks the row the pointer passes over, and `gmove.py` glides diagonally first** (Q356): from the hire
  screen's tab (667,123) to (900,700) it crosses rows 0 to 4 and leaves the fifth picked. To put the pointer away
  from a list, go straight across, then straight down, outside it: `gmove.py 1000 123`, then `gmove.py 1000 700`.
  To hover ONE row (Q376, `q376/orig/run.sh`'s `row`): from there across to (105,700), up to the row's band at x 105,
  left of every list, then straight in to x 250; the visitors' rows stand at y 170 + 27.9k, all items' at 222 + 20.4k.
  `rectcolour.py` reads the highlight on a 1024 x 768 frame in virtual units: (300, 2y-16, 680, 2y+16) for a visitors
  row centred at y, (275, 432 + 40.8k, 640, 468 + 40.8k) for an items row, (300, 420 + 39k, 700, 452 + 39k) for an
  all-staff row in either game's frame (Q382). A right click on the all-staff list below its rows: (300,700), then up to
  (300,360), crossing no row of four (Q382, `q382/orig/run.sh`).
  The hire screen: the gadget's Buy (113,590), then the buy screen's b_allstaff (958,317); the mechanics' tab (667,123).
  All staff and all items (Q365): the keys S and A (`xdotool keydown`/`keyup` on `:77`; they act on the release, from
  the park or over a screen); all staff's mechanics' tab (665,121), all items' shops' tab (786,123). All staff's five
  tabs (Q369, `q369/orig/run.sh`): (605,121), (665,121), (724,121), (783,121), (843,121). Its headings (Q381, `q381/orig/run.sh`): Name (252,180),
  Current Status (432,180), Monthly Wage (558,180), reached straight down from (x,123); this game's are controls
  `0x10` to `0x14`. To hire several of a kind here, read `candidates`: it letters handymen **"Cleaner"**. A member's skill and
  happiness in its memory: `thingwords.py` on `T:0x1e4:I` (grade), `T:0x1e8:B` (percentage), `T:0x1f8:f`
  (happiness), started after the load with its start stamped, since its times count from its own start. All items' four tabs (Q370, `q370/orig/run.sh`): rides (667,123), sideshows (726,123), shops
  (786,123), misc (845,123); V opens the visitors over a screen. All items' Name heading (Q374, `q374/orig/run.sh`):
  (341,185), reached straight across from (341,123); its rows from y 223, 20.6 apart, a right click on the row the
  pointer was last brought down to. The stock park holds one shop and one sideshow: `buy` the catalogue's (`catalogue`)
  smallest name below the stock one to make a row that is added last and must sort first (`q374/confirm.py`). A guest's row in the original's memory (Q371, `q371/orig/run.sh` with `POLL=1`): the number `+0x1d8`, the cash
  `+0x1a0`, the rides `+0x1c4`, the happiness the float `+0x19c` (`thingwords.py ... 38:0x19c:f`); in a park file the
  guest record's `+402` (`mArrivalIndex`), `+414` (cash), `+422` (happiness, a float), for `parkfile.thing_set`. The
  original's visitors rows stand 55.8 units apart from (virtual) y 340. **The reference original's resolution
  sets its font set** ("The original under Proton", "Resolution"; Q377): at its standing 800 x 600 a list's row height
  and a font's size are this game's at its 1280 x 720 window. **Its rows change two seconds after V**: the
  rewrite is not the adder (the time in park four times, Q372), so a frame of what the opening fill letters is taken
  within 2 s of the key, before moving the pointer away (`q372/orig/run.sh` with `EARLY=1`: 0.58 s). The guest's
  `mArrivalDate` is `+0x1d4` live and `+398` in a file; poll a happiness beside it, which moves, for the clock.
- **The shortcuts' keys in the original** (Q366, `q366/orig/run.sh`): B H I A S V F L T G and Space plain, S and V with
  Ctrl held (`xdotool keydown Control_L` around the key). **L opens nothing in Instant Action**, so an Escape sent after it
  opens the game menu and the next key falls on the menu: close a screen only where one opened. The gadget's
  Information button (173,596) and Money button (107,650) of 1024 x 768 (halve both the stream's x and y).
- **The locators** (Q424, `q420/orig/run.sh`): Ctrl+S or Ctrl+V as above, then the buttons on the arm at handymen
  (303,613), mechanics (360,613), guards (416,613), entertainers (332,670), scientists (388,670), visitors (359,641)
  (the mechanics' and the visitors' clicked, the rest the same layout's); its words are the locator `[0x007cc2f0]`,
  the role `[0x007cc30c]`, the person `[0x007cc308]` (a thing's address: its type byte `+2`, `+0x190`/`+0x194`, a
  visitor's number `+0x1d8`) and `gui_CameraFlags` `[0x00790ab0]` bit `0x1000`, read with `tpwmem.py read`. In this
  game the console's `locator`, and `control 95f` for a button's place. A camera against a walker's place is read
  under `pause`: two replies apart, he has walked most of a cell. **After Ctrl+S over its own locator, Escape opens the game
  menu** (seen here; the original's `FUN_004816b0` asks only an open locator): fold the panel with Ctrl+V instead. The advisor's
  lines `0xc6` and `0x11f` are `advisor.py %PID% SECS 0xc6,0x11f` in the original, the log's `message=198` and `287` here
  (Q425, `q425/confirm.py`, `q425/orig/run.sh`). A fenced member's patrol edges are `squares.py %PID% 4` after the
  kind's button in the original, and here `locator`'s `patrol=` with `strip`'s drawn census, `| patrol N x,y,marker^lift`,
  read under `pause`; the stock mechanic and entertainer are fenced, the researcher not (Q426, `q426/`).
- **What a world click is, in either game** (Q444, `q444/orig/run.sh`, `q444/confirm.py`): the original's hover category is
  `[0x007c24f4]` (`park-engine.md`'s table: 0 nothing, 1 path, 2 ground, 7 a guest, 8 to 12 a member) and the person under
  the pointer `[0x007b05ac]`, read with `tpwmem.py read` after a `gmove.py` glide; with a locator's follow on, the person
  is near (512,384) of 1024 x 768. **Click at once on the point that reads the category wanted**: a few seconds of reads
  and a frame between let a walker go (round a). Here `pick` names the cell, the person taken by their picture
  (`standing thing N`) and the cell's object apart (`object thing M`; Q474), `cell X Y` its `occupant` and `parent` (a
  queue cell's parent is its ride too: test the type), `tool` the mode as a number (`mode 0,` none, `mode 1,` the path
  tool); a press of any length lands, one let go inside a slow frame too (Q476; `button(1, what, held=0)` is a press let
  go at once, the original's `xdotool mousedown 1 mouseup 1`); for a guest or a member to stay put, `pause` once the arm has stopped (its slide is eased on `Time.Delta`), click, `resume`. A window's
  OK on a 16:9 window is the 2048 x 1536 rect scaled by the height and moved in by the side bars. **Where a member stood** (Q446, `q446/confirm.py`, `q446/orig/run.sh`): the stock park's guard 28 is alone on (39,28) in the file and walks off it within a minute in both games (the original's paces to (39,25) and back); the guards' button opens his locator, and the original's pixel over a cell with a locator up is `PICK=1 cellfind.py` (its build preview reads (0, 0) then); wait with `cellhead.py` until he is near the cell and off it, or the pixel can land at the frame's edge under the HUD.
- **Ctrl+click and a put-down in both games** (Q635, `q635/orig/run.sh`, `run-c.sh`, `q635/confirm.py`): the original off-screen
  by `loadfile.sh`, `setcam.py PID 470,270 125 180 off`, each cell's pixel by `PICK=1 cellfind.py`; Ctrl is
  `xdotool keydown Control_L` around a 0.2 s press. Read the tool `[0x0081ae2c]` (4 an item in the hand, 3 the queue
  tool), the item `[0x008186e0]`, the rotation `[0x0081d7a4]`, the cursor `[0x007cb2d8]:H` (8 `c_noplace`: test a cell
  with it before a put-down, as `run-c.sh` does, for a ride's footprint reaches over the path two cells off) and
  `bank.py` either side; the gate's takings land 10 or 20 at a time between reads. Here the same view is
  `camera 470 270 125 -180` (`lib/bothcam.py`), cells by `cellprobe.py`'s `find` under `tool path`, Ctrl by
  `key_down( "Control_L" )` around `button( 1 )`, and `hand` prints `hand: item N` (an empty hand is `item 0`).
  **One camera per target** (Q672, `q672/confirm.py`'s `aim`, `look` and `over`): at 1280 x 720 one shared view lost a
  target off the frame twice, so each cell is found with the camera on it (`camera X*10+5 Y*10+5 110 0`, `step 10`), every
  pixel before anything is carried (`find` arms `tool path`, which lets the hand go), and each click goes back to its own
  cell's camera. Every target then sits at the window's middle, so a pointer moved to the same pixel after a camera move
  does not move and the hovered cell, and the carried squares drawn from it, stay the old view's: nudge it a few pixels
  and back, then `step`, before a photograph. Read `tool`'s mode number, not its words: the idle reply carries its
  usage, "`tool queue <thingId>`". The original's half is `q672/orig/run.sh` (`setcam.py` on each target).
- **The camcorder button in the original** (Q428, `q428/orig/run.sh`): the button (130,707) and, once its panel is out,
  the camera button (283,641), of 1024 x 768; its line `0x131` is `advisor.py %PID% SECS 0x131`, here the log's
  `message=305` (`q428/confirm.py` clicks `control 27`, `64` and `63`). **The original's camera button installs a pick**
  (a camcorder cursor over the orbit view, a right click cancels it), not first person. **Leave its postcard button
  out**: it pauses the game and writes a picture. **Into first person** (Q496, `q496/orig/run.sh`): the stock park with
  `setcam.py %PID% 450,165 110 180 off`, the pick, then a left click on the red path inside the gate (290, 395):
  `helpbar.py`'s `flags` reads `0x202`. A click on the hoarding (617, 378), or on the land at (700, 450) or (617, 500),
  is refused (the no-entry cursor, seen at (617, 500)) and leaves the pick up; one at (512, 420) or (450, 450) drops the
  pick back to the orbit's mode `0x6fea10` without first person (`q496/orig/a-run.out`); C puts the pick back. **The pick over a ride** (Q497, `q497/orig/run.sh`):
  Q265's `bounce.TPWS` (`q265/run-fix1/`) in its saved view, no `setcam.py`: the Belly Bounce at (600, 470), path at
  (300, 300) (the pick's click there enters first person), ground at (400, 440), though in Q498's round c that point
  read the Belly Bounce (cat 4) in orbit: read A1 before trusting it; `SCAN=1 run.sh` reads the hover's
  category over a grid in orbit to find such points in another file. **Ours into first person** (`q498/confirm.py`):
  `control 27` then `63` enter it at once (no pick, Q25); the eject button is `control 89db53`; `pointer` reads the
  cursor and the row.
- **The game menu:** Escape with no tool in hand opens it **and pauses the game**; never sleep under it. Load
  (510,50) then the first row (400,123), the newest file; Save (510,140), type the name, Return; Options
  (510,365 at the install's 800 x 600), the quality track (512,276), OK (898,688).
- **Its graphics quality** (Q487): the Options screen as above; one click on the track left of its knob, at (512,276),
  took it from Medium to "Graphics quality: Low" (the knob did not move, the words did), and its minus at (491,276) did
  nothing. OK writes the dword at byte 12 of `save/Config.tcf`; the detail file is applied only as the game starts
  (`Window_Create`), so stop the original (`loadfile.sh stop`) and load again before measuring. Copy `Config.tcf` first
  and put it back, with its time (`touch -d`), after; a second `loadfile.sh load` writes its own "before" sum with the
  new quality in it, so compare `save/` with a sum taken before the first (`q487/orig/lib.sh`, `a-options-low2.png`).
  **Quicker, with no Options screen** (Q516): write the dword at byte 12 of `save/Config.tcf` yourself (`printf
  '\x00\x00\x00\x00' | dd of=Config.tcf bs=1 seek=12 count=4 conv=notrunc`, after `cp -p` of the file) before
  `loadfile.sh load` starts the original: the word read `0x1025900`, low's (`q516/orig/`, `q516/PREDICTION.txt` P9).
- **Buying:** Buy (117,588), a ride's row such as (560,305), the features tab (843,123). A blue footprint is a
  legal place and a red one is not. After the placing click the queue tool is in the hand: a click on the path
  joins the queue and puts the tool away, and a later click on the queue's cell picks it up again (Q257o).
- **Hiring:** Buy (117,588), the side tab (957,316), mechanics (665,123).
- **A shop bought in the original, and whose meshes it hides, in both games** (Q658, `q658/orig/`, `q658/confirm.py`):
  Buy (117,588), the shops tab (784,123), a row at y 222 + 19.5k; a shop the file has not researched is not listed,
  so `original/research.py <in> <out> 1212` first (the Fries Shop in `late30.TPWS`: its row then (556,281)). Put the
  camera back (`setcam.py`) after the row, then click the ground. `modelnodes.py PID =fries` reads each mesh's word
  (`0x10` hidden; a light cache of nought, never drawn); here `meshes THING` names the drawn meshes put away and channel
  0's start (`bound Bind { Start, Flags }`).
- **A clip's phase is held by its start stamp**, a reading of the saved clock that steps by the clip's length each
  loop (a boat's: 534 ms): compare a stamp with a file's less whole loops, never a frame number (Q257t).
- **Breaking a ride:** `scriptvar.py PID <thing> 4 1` writes `VAR_BREAKSTAT` (variable 4 of every ride's
  common twelve). Write it inside a go: only a car carrying riders smokes, and the go ends at once. Its
  mechanic mends the ride about 25 s later, so save (the menu pauses the game) before then; the mended file is
  evidence too. A script's variables are the dwords at `[frame + 0x1c]`, not `+0x20` (Q257x). As a `POLL` of
  `loadfile.sh` on `q257o/orig/hotpot-later2.TPWS`, `sleep 3.5; scriptvar.py %PID% 43 4 1` lands inside its go,
  and the load's own frame at 15 s shows the pot broken, on the saved view (Q259).
- **A broken ride is mended fast:** loaded from a file, a broken Hot Pot had its mechanic assigned two ticks on and
  was open again 32 ticks (8 s) after the load. Poll from before the click (`loadfile.sh`'s `POLL`); the poll's first
  line is the park before the load, its second the file's (Q257y).
- **Its object chain is not in id order:** the shipped park's runs 15, 24, 23 down to 16, then 14 to 11, and a
  thing bought goes on the front. Read an order from the file's `mNext` links, never from the ids (Q257z).
- **Instant Action offers only researched items:** patch a copy with `research.py`, never the reference
  `easymode.TPWI` (Q257n).
- **Its music off:** Escape for the game menu, its Options (510,365 at the install's 800 x 600), the Music switch
  (445,533), OK (898,688), which goes straight back to the park (an Escape after it opens the menu again, and
  (445,533) on the menu is Exit To Lobby, which writes an autosave: Q442); the same clicks put it back, and
  `save/Config.tcf` then holds the same bytes. A park's mix still reads 0.04 rms with it off and every placed voice
  silenced, so one placed sound's loudness is not read from the mix: neither a match of its sample (0.04) nor its
  loop's length in the autocovariance found a waterfall 21 units off (Q263).
- **How loud its mixer plays a voice** is read, not captured: `mixer.py PID` after a load prints each channel's
  distance and gain (Q263's camera files `q263/files/aim-*.TPWS` step the listener away from the waterfall). A
  channel marked `dirty` has not been worked out since it last moved: read the ones that are not (Q264).
- **Its mixer over a ride's screams:** a file saved under `parkrun.original_view( 515, 235 )` puts its listener
  about 50 units from the Belly Bounce, and `POLL='python3 .../mixer.py %PID% 150'` reads the scream channels (a
  mapping of 2.0, 95.0, 0.80 at (525, 250, 0)) and the staff's as they come and go (Q265).
- **Its crowd's voice in the mixer:** start `mixer.py` and `q236/orig/crowd.py` together after a load and move
  the pointer on and off the Belly Bounce's queue (`gmove.py 512 384` mid-frame on Q265's `bounce.TPWS`, then
  `930 200`): the crowd's is the 2D channel at the listener's own place, there while the handle is held and
  gone while it is nought. Neither log is stamped, so hold them together by the order of their stretches
  (`q268/orig/run.sh`, Q268).
- **Its screams' pitches:** `mixfreq.py PID 150` after a load of Q265's `bounce.TPWS` reads the Belly Bounce's
  scream channels (far end 95, 35 of 127, at (525,250,0)), 180 lines in 150 s (`q297/orig/run.sh`, Q297).
- **Its guests put off, and their pitches:** `breakq.py PID 330 13 all` beside `mixfreq.py PID 330` on Q265's
  `bounce.TPWS`: the put-off's channels are the 3D ones with a far end of 100 at 10 of 127, 39 lines in nine
  breaks. Broken on a timer instead, the Bounce's queue is empty at most breaks, and without `all` only a guest
  whose id divides by eight sounds: two lines in 270 s (`q298/orig/run2.sh`, Q298).
- **Its Hot Pot's engine in the mixer:** `mixfreq.py PID 150` as the `POLL` of a load of
  `q257o/orig/hotpot-later2.TPWS` (a pot in its go): the engine is the 3D channel at a far end of 100 that moves
  inside the pot (x 426 to 456, z 244 to 252), 51 of 127, some 2,000 lines as its frequency follows the boat's
  speed; a fixed channel at (435, 255) is another sound (`q299/orig/run.sh`, Q299).
- **Its thunder, without a storm:** `thingpoke.py PID 150 15 7:0x38:B=1` beside `mixfreq.py PID 180` on Q265's
  `bounce.TPWS`: a bolt every five seconds or so, each thunder a 3D channel at a far end of 1500 and a height of
  300, 30 in 150 s. The game writes the flag back to nought every 163 ticks, as its weather changes, so it must be
  held, not written once (`q300/orig/run.sh`, Q300).
- **Its weather, chosen:** hold the weather thing's FORECAST, not its quality: `thingpoke.py PID 50 15 7:0x28:i=1`
  makes its next change the heaviest rain (and lightning), `=20` and `=35` lighter rain with none, `=100` dry. A
  quality written straight to `+0xc` changes nothing, since only a delivered forecast is turned into drops
  (`FUN_005127a0`). A change came inside 35 s of each hold, the computer on seven days; `rainmix.py` beside it
  reads the drops, both rain voices and their channels (`q301/orig/run.sh`, Q301).
- **Its gulls and its sea:** a file saved here under `camera 400 60 110 180` puts its listener at (417, 47, 18),
  inside both rectangles; `camera 400 100`, `400 140` and `400 200` step it to 15, 75 and 136 from the gulls'
  (`q302/files/sea-*.TPWS`). `voices.py` shows effects 2 and 3 (flags word `0x0006`, voice flag `0x1000`) with
  their rectangles, and `mixfreq.py` their channels at the voices' places: 37 of 127 the gulls, 25 or 41 the
  sea, a far end of 100. A second effect 2, flagged `0x0606` at (0,0,0), is the music (`q302/orig/run.sh`, Q302).
- **Its Belly Bounce worn out by itself:** `object.py PID 840 13 5 4 7 8 9` as the `POLL` of a load of Q265's
  `bounce.TPWS`, left alone: a fall of the State of repair every 64 ticks, `VAR_WORN` under 20, the break some
  4,500 ticks after the load (13 minutes at seven days' uptime) and the mend 126 ticks later.
  `q303/steps.py <log>` prints a line a fall with the riders that give it (`q303/orig/run.sh`, Q303).
- **Its advisor told of a worn ride:** `advisor.py PID 1100 0x42 13` as the `POLL` of a load of Q265's
  `bounce.TPWS`, left alone: message `0x42`'s record goes from `(0, -1, 0, 0)` to `(tick, 0, 1, 0)` on the fall
  of the State of repair that leaves its whole number under 10, about 13 minutes in; grab `:77` a second later
  for him in his hard hat (`q305/orig/run.sh`, Q305). Here the stock park left alone does the same in 15 to 22
  minutes: grep the log for `Park advisor: post message=66` (`q305/confirm.py`).
- **Its advisor told a mechanic is on his way, inside a minute:** as the `POLL` of a load of Q265's
  `bounce.TPWS`, `advisor.py %PID% 90 0x46,0x42 13 & object.py %PID% 90 13 4 7 & sleep 20; scriptvar.py %PID% 13
  4 1; wait`: one log, both pollers' lines stamped with the tick. Seven ticks after the write the Bounce's
  `staff` reads 26 and message `0x46`'s record `(tick, 0, 1, 0)` (`q306/orig/run.sh`, Q306). Here
  `scriptvar 13 VAR_BREAKSTAT 1` does the same in two seconds: grep the
  log for `Park advisor: post message=70` (`q306/confirm.py`).
- **Its advisor told no mechanic can come:** *none in the park*: fire the mechanic HERE (`fire 26`), `savepark`,
  and load that file in the original (it reads a park with no mechanic as its own); the same `POLL` with
  `0x44,0x45,0x46` has `0x44`'s record said on the tick the Bounce's state reads 1. *Every mechanic busy*: the
  original's own `q257o/orig/hotpot-later2.TPWS`, `scriptvar.py %PID% 43 4 1` 3.5 s in and `scriptvar.py %PID%
  13 4 1` six seconds on, `thingwords.py %PID% 80 26:0x19c:I:state 26:0x218:H:job` beside the pollers: a slot
  holds `0x45` with the Bounce as the first line is still said (`q307/orig/run.sh none|busy`, Q307). Here
  `parkscene.broken_pot()` then `scriptvar 13 VAR_BREAKSTAT 1` once the log reads `mechanic 26 found ride`
  (`q307/confirm.py`).
- **The advisor in a hat, inside a minute:** the same break (`scriptvar 13 VAR_BREAKSTAT 1` 25 s into the stock
  park) has him say response 204 in the hard hat two seconds on; grep the log for `Advisor: sample 20` and read
  `wearing=` in `advisor`. His head is in the frame's lower right: `q308/confirm.py` counts the hat's pixels in
  the box from 0.77 to 0.93 across and 0.55 to 0.78 down, where R > 200, G > 180, B < 170 and R - B > 60 read
  6307 with the hat and nought without (Q308).
- **The theme's own advisor in his costume, inside a minute:** load `q345/nofeat30.TPWS` (the stock park with no
  feature, saved on tick 1584): row 81's line, a slot 1 response with the prop -1, is said on tick 1593, nine
  ticks in, in both games; grep the log for `on model slot 1` and read `wearing=-1,0 slot=1` in `advisor`. Break
  the Bounce once `advisor` reads `shown=False` for a slot 0 line straight after it (`q346/confirm.py
  fix|control`; the original's frames of the file are `q345/orig/a-g*.png`). `q346/confirm.py` counts the
  helmet's pixels in the box from 0.70 to 0.95 across and 0.62 to 0.85 down, where R > 150, G > 110, B < 170 and
  R - B > 50 read 5862 with the helmet and nought with the lobby's fly bare-headed; do not look for `slot=` alone
  in `advisor`, whose park half prints the advisor's eight slots (Q346).
- **A park file with a tag in it, and what a load does with it:** break the Bounce as above, wait for the
  line, `savepark`: `parkfile.py -t <file>` prints the block's records, and five of the original's own files hold
  one to set beside it (`q257x/orig/broken-by-the-original.TPWS`, id 72 about a Hot Pot; `q253/orig/
  sold-by-the-original.TPWS`; Alexah's two jungle saves). **Start the original on a file with NO tag, then
  `loadfile.sh` the tagged one with `POLL` `tags.py`:** it then prints none, one, none. Started on the tagged
  file itself, the park it enters already holds the tag, and the poll's lines do not change at the load (Q310).
- **A tag on the aerial, inside a minute:** the same break has the mechanic's line leave tag 72 on the gadget's
  mast for 30.5 s of real time; `tags` reads the list and the mast's, the top's and each tag's rectangle, and
  `q309/confirm.py`'s `topedge` finds the red top's upper edge in a frame (35 rows up at 720 down). In the
  original `tags.py` beside `advisor.py` reads the same rectangles. **`loadfile.sh` comes back some twenty
  seconds after its click**, so a frame taken as it returns is not a "before": use its own `-2-loaded.png`. And
  with no tag the red top rests over the lowest tag's place, so a colour count in that box says little (Q309).
- **The advisor cut off his line, in both games:** the same break and click post message `0x11c` (the message
  bar's own tutorial line, 13 s long), and the tag clicked again while he says it withdraws it. In the original
  `advisor.py %PID% 150 0x11c,0x46` reads the slot, then the record's said byte and its slaps, and the action's
  length going to nought (`q312/orig/run.sh b`); here the log's `Park advisor: post`, `attempt` and `withdraw`
  lines and `advisor`'s `said=/slaps=` (`q312/confirm.py`). The console's `tutorial on|off` is the Options
  switch in memory. **Take a log mark BEFORE the click whose line is wanted**, not after its reply (Q312).
- **A tag opened on the message bar:** in the original, the tag stands at (84, 909, 154, 974) of the 2048 x 1536
  layout, so at 1024 x 768 `gmove.py 59 471` and a click opens it; with the bar out its first button is at
  (283, 586) and its erase button at (283, 694), and the camcorder button at (130, 707). `bar.py` beside
  `tags.py` reads every step (`q311/orig/run.sh`: the break at 20 s, the tag four seconds after it comes). Here
  the console's `click X Y` takes window pixels: a layout point is `x * h / 1536` across and
  `h - (1536 - y) * h / 1536` down, the tag (56, 441) at 1280 x 720, the bar's buttons (265, 549) and
  (265, 650); `tags` prints the bar's line. **A `reply` waits its whole time before the next command**, so to
  read what a click did at once give the click 0.02 s (`q311/confirm.py`, Q311).
- **The arm's slide, step by step** (Q427): here, click under `pause` and `step` the frames (16.7 ms each against the
  arm's 20 ms timer; 2, 4, 7, 10 and 14 frames are 1, 3, 5, 8 and 10 steps, clear of the boundaries), `tags` reading
  `handle=`, `move=`, `carried=` and `waiting=` after each (`q427/confirm.py`); `carried=` ends `/depth=N/behind body
  N retract N of N` (how many of the panel's shown parts draw before the body and before the cross) and `retract=` reads
  `on`, `off` or `hidden` (Q445, `q445/confirm.py`). The console's `control` takes the id in
  hex (`control 27` is the camcorder button). In the original, `bar.py` polls each step and `record.sh` films it; a
  second action inside the 0.22 s slide is one `xdotool` call after the click's release (`mouseup 1 sleep 0.03 keydown
  Control_L keydown s ...`, `q427/orig/run.sh` with `C=1`). Break the ride AFTER `loadfile.sh` comes back, not inside
  its `POLL`: it came back 85 s into the poll there, past the tag's 30.5 s life.
- **Its sound is not paced off-screen:** the null device takes the mix as fast as it is made, so a held voice's
  samples follow one another in a third of their clips' length. Read volumes, pitches and variations from its
  mixer, never a pace or a sample's length in time (Q269).
- **A mixer channel's frequency** is the word at `+0x1f4` of its record (22050 for the kids' samples at pitch 0);
  `q269/orig/probe.py` pickles a channel's whole record a poll, which is how a word of it is found (Q269).
- **A camera file's listener is not where the camera was aimed:** `camera 531 513 110 180` saved here reads a
  listener of (546, 50, 476) there. Read it (`voices.py`'s first line) rather than work it out (Q263).
- **A point's place on its screen** is its park place (x, the height, z) through the matrix at `[0x0087b088]`,
  then `x / w` and `y / w` as the screen's -1 to 1 (`spritepass.py` does it): how to find a thing in its frame, or
  to prove a drawing rule on it (Q258).
- **A highest or widest reading grows with the time looked.** Compare the two games over the same time, or
  predict a count, never a maximum (Q258: a bubble's height read 31 in ten seconds and 42 in forty).
- **For the first seconds after a load its camera is not on the saved view**, so a thing that lasts under five
  seconds from the load is in its memory and not in its picture: poll it, do not film it (Q257w).
- **Both games at one camera, aiming, the follow and the lens:** "Cameras in both games", above (Q435, Q436).
- **`tpwmem.py read` and `watch` take every address as hex**, with or without `0x`: a decimal one is silently
  another address, so work a sum out first.
- **A ferry or seaplane made, and a file that holds it:** load `q340/late30.TPWS` (a leaver at the stop, no
  vehicle current): the leavers' summons comes on tick 1093 and, since Q443, draws from the file's seed 0x3f416bba: the bus first in every run, which makes nothing (the seaplane is the next summons, 0x3e8846bc -> 0x975cef2b); for the ferry, write a seed whose first draw `% 3` is 2 into a copy. Save while the vehicle is current (before tick 1200);
  `parkfile.py -v <file>` prints the three slots, the object list, cell (0,0)'s chain and each vehicle's record,
  and the original's own to set beside it are in `q257x/orig/fixed-by-the-original.TPWS`. In the original,
  `worldwords.py` on the four vehicle words and `object.py %PID% SECS <thing> 0 1` (the vehicle script's
  status and trigger) say whether it kept the vehicle: `q342/orig/run.sh` (Q342). **The ferry in the original:**
  `lib/seedfile.py q340/late30.TPWS <out> 3f416bba 1` (`q583/orig/seed1.TPWS`): the ferry made thing 14 on 1094,
  status 2 on 1174, 4 on 1209; `q583/orig/run.sh <tag> <file> 14` films the harbour at 540,-20 zoom 130 (Q583). This
  game's file with the ferry made (`q342/ferry43.TPWS`) runs the ferry's script in the original, which draws no ferry.
  **The seaplane in the original, and its second visit:** `q622/orig/seed3.TPWS` (`lib/seedfile.py` seed 3; late30 as it
  is drew the ferry twice and seed 2 the bus): the seaplane made thing 14 on 1094, status 2 on 1141; `original/retrigger.py`
  in the `POLL` sends it round once it is spent (2 again on 1272), `q622/orig/run.sh <tag> [SECS]` with `FILE=` (Q622).
- **A vehicle at its stops in this game:** `q583/confirm.py <out> <bin> seaplane|ferry|bus` (`load 40`, `61`, `10` on the
  stock park): runs free to near the status, then `step 5` until `vehicles` reads it; the line ends `body (x, y, z)
  nose (x, y, z)`, the routed node in its model's axes (Q583). `parkrun.py`'s shared folder (`q241f/game`) has held a
  stray `autosave.TPWS` that `enter()` loads before the stock park: give the run its own folder (`gamefolder.make`,
  `GAME_FOLDER`) and leave the autosave out of the copy.
- **`parkfile.models()` is keyed by a model's HANDLE**, the object record's `+54`, which is its slot plus one:
  taking one off reads the record before it (Q342).
- **A park whose guests need a toilet, with none standing:** `lib/stockscene.py ... 960 "sell 21" "sell 22" "sell
  23" "toilet 70"` (the stock park's toilets are things 21 to 23, its thirteen guests 29 and 31 to 42, all inside
  on tick 962; `toilet <n> <guest>` sets one). The advisor's row 82 is polled on tick 974 (819 and every 155 on)
  and says sample 225 where over a quarter of the guests inside are over 60. Only guests whose id divides by
  four (32, 36, 40) gain a point every sixteen ticks, in both games, and guests come and go, so the share
  moves by itself in a long run: read it with `original/inside.py` or
  this game's poll line. A runtime cell's record in the original is `[0x008023a0] + (y * 128 + x) * 68`, the
  packed id LESS ONE (Q347).
- **An advisor line that is Full Simulation's alone, said in the stock Instant Action park, in both games**
  (Q348): read the row's `+0x0c` in `meta_full.json` FIRST (nought: the poll scores it in game type 0 alone, and
  31 polled rows are so). This game: the console's `advisorgame full` given paused after the load, before the
  row's turn (`advisorgame own` lets go). The original: `bytehold.py %PID% 47 0x00fb3b7c=2 1112 0x00fb3b7c=0`,
  then `bytehold.py %PID% 4 0x00fb3b7c=2` to put it back, holds the game type at 0 from tick 1113 over the row's
  turn; never through the load, which reads the game type for its catalogue, so use the row's second turn after
  the load (155 ticks on, 41.5 s from the click). Every Full Simulation row polled inside the hold is scored too:
  name them to `advisor.py`. The stock park's eight features on offer all standing: `lib/stockscene.py ... 960
  "buy 1401 45 25 0" "buy 1425 46 25 0" "buy 1428 45 26 0"`; its researcher is thing 30 and its lab thing 6,
  whose projects `thingwords.py %PID% SECS 6:0x145c:I:projects 6:0xbc:f:done8` reads (project n's `mCost` is at
  `0x18 + 20 n`, its `mResearchDone` four on) and `parkfile.thing_set( src, dst, 6, 16 + 16 * n + 12, '<f', cost
  )` finishes in a file.
- **The bank in both games: the fee, a bought loan, a balance under nought** (Q524, `q524/confirm.py`, `q524/orig/run.sh`):
  `parkfile.economy_set( src, dst, dict( balance = 100 ), {(4, 'bought'): 1, (4, 'repaid'): 5} )` makes a park with a
  loan running (no screen here buys one); here `screen 10`, then the spinner's plus at 0.907 across and 0.30 down
  `control 4f3ae`'s place moves the fee by one a click, `clock 17556` and `step`s turn the month on 17557 (the loan's
  750 and the wages, and, from a park loaded here, a year's change too, which zeroes the profit: `SetCounter` leaves the calendar's year at 2000), and the console's `loans` prints the bank and every slot. The original's bank in memory is thing 8:
  `thingwords.py %PID% SECS 8:0xc:i:balance 8:0x118:i:fee 8:0x11c:i:last 8:0x120:i:red 8:0x124:i:profit`, loan n's
  `loan_bought` at `0x28 + 0x20 n` and `months_repaid` four on; its Money button (107, 645) opens the entry-price screen.
- **The weather in both games: a storm saved, a forecast pending** (Q528, `q528/confirm.py`, `q528/orig/run.sh`):
  here, under pause, `clock T` then `weather Q` (a quality, 1 the worst; it drops a pending forecast and rebases the
  change), `bolt X Y` (a bolt at its foot, arming the thunder), then `step 8` a weather tick at a time; the census
  `weather` ends `as saved: ...`, what a save would write. `clock`'s jump moves this game's day count by one edge,
  counted on the first advance after it, so a forecast comes three edges after the day `weather` was given on. The
  original's weather in memory is thing 7: `thingwords.py %PID% SECS 7:0xc:i:quality 7:0x30:i:drops 7:0x34:i:target
  7:0x38:B:lightning 7:0x44:i:thunder 7:0x2c:B:valid 7:0x28:i:fq 7:0x5c:f:wind 7:0x54:I:voice`; it schedules on the
  first tick past each instant, so its ticks are predicted exactly. Its rain is drawn only on a step of the drops'
  ramp: a file loaded with its drops within 20 of the target rains nowhere until the next change, so film the change
  (`record.sh start NAME 10` inside `loadfile.sh`'s `POLL`, `q528/orig/rerun-e.sh`) rather than time stills by its tick:
  a list's load returns some 20 s after its click.
- **The skill bars and the hire screen's, in both games, at chosen grades** (Q353): write the five stock staff's
  grades into a file (`parkfile.thing_set( src, dst, thing, 398, '<i', grade )` for things 25, 26, 27, 28, 30;
  `q353/grades.TPWS` is 4, 1, 0, 3, 2). In the original after `loadfile.sh` (1024 x 768 points): the Money button
  (107, 645), the entry-price screen's staff-costs button (810, 177): the training screen; the Buy button (113,
  590), the buy screen's b_allstaff (958, 317): the hire screen; the hire list's first row (693, 231), with no
  press, picks it; the mechanics' tab (667, 123) (`q353/orig/run.sh`). In this game the same through
  `q353/confirm.py`, the control's place by the console's `control 26`; a click on a hire row CHOOSES the
  candidate and closes the screen, so hover. `lib/barshare.py` measures each bar.
- **A park file entered through `screenpark.sh` (the player's click) is framed on the camera's own point**, not 65
  units on as `parkrun.original_view` gives for the Load Park list: `camera 585 235 110 180` put world (585, 241)
  mid-frame and `camera 595 175 110 180` the Staff Room (Q359, `q359/orig/e-2.png`, `f-2.png`); screen x runs against
  world x there. Under `loadfile.sh` the view is the list's, nearer again.
- **Aiming a poke of the original's ground cells** (Q360): the view `loadfile.sh` leaves on Q359's file is about
  ninety pixels a cell, and screen x runs AGAINST the cell's x (the Belly Bounce at x 51..53 mid-frame, the path at x 56
  left of it). Read the visible range first: the terrain object `[0x00879d60]`, rows `+0x128`..`+0x12c`, a row's cells
  `+0x130 + r`..`+0x1b0 + r` (signed bytes); then `groundcell.py` with `READ=` over the box, and poke inside both. Grab
  the off-screen original's frames as `loadfile.sh` does (`import -window root -display :77`), a pair before for noise.
- **A model's materials, their textures' alpha and their `0x2` bit, predicted from the files:** `q359/predict.py
  [model ...]` (from `q359/materials.txt`, `wct2raw` run over every jungle, lobby and global advisor model ONE AT A
  TIME: parallel runs abort and thin the list) gives the line each model logs at its load; `q359/flips.py` lists the
  `0x2` uses whose reference the header moves (Q359).
- **Lost Kingdom's river in both games** (Q358): `camera 480 465 110 180` (`parkrun.py`'s `original_view( 480, 400 )`),
  `step 2`, then `save()`; `original/screenpark.sh` on that file. The original's saved view stands nearer than this
  game's and frames the long reach (78 water pixels across at x 300 of its 1024 x 768 frame, this game's 36 at x 530; measured before Q441's lens, so re-measure before relying on the pixel counts),
  not the left bend. Measure a bank by `lib/seam.py` (this game's window x 430..630, y 230..330; the original's
  x 100..450, y 200..420), never by a fixed darkness level: the original's terrain is lit darker and its rocks'
  outlines touch the water. Every saved file of the same scene differs in bytes (the park runs before the pause).
  The lobby's player slot opens only Lost Kingdom for the reference player: the other islands are seen far off.
- **A list sorted in the original, whose values move as its park runs** (Q373): stamp the heading's click in the run's
  log (`echo "== click $(date +%s.%N)"`) beside `thingwords.py`'s poll, and judge the ORDER by the values polled at the
  click and each row's picture by those at the last two-second rewrite before the frame: the rewrite changes a row's
  value in place and moves nothing, so a thought that changed after the click stands out of order (`q373/orig/b`). The
  "?" heading is the 1024 x 768 point (711, 128).
- **A member of staff held in a state, in both games** (Q379): write the state into a copy of the file
  (`parkfile.thing_set( src, dst, thing, 483, '<i', state )`, `q379/mkscene.py`), load it, and in the original hold
  `+0x19c` with `thingpoke.py PID 60 <type> <thing>:0x19c:I=<state>`, one call a kind (type 5 handyman, 4 mechanic, 6
  entertainer, 7 guard, 8 researcher). Three traps, each a run: a strike (4, 5) ends at once unless the staff HQ's flag
  for the kind is set (thing 1, `+22 + 12 x kind + 4`); **state 2 or 3 with no rest area faults the original**
  (`FUN_00505fe0`, `FUN_00506d10` read thing `[+0x208]`): use 3 with the rest written (`+499`) and held (`+0x1fc:f`) at 10
  so the rest never ends; and this game's sweeps before `load()`'s pause end an idle or a job whose stamp is long past.
- **A member of staff at a linked walk's dead end, in this game** (Q380): paused in the stock park, `delpath 48 22`
  cuts the Belly Bounce's queue tail and leaves its back cell (49,22) one link, the one its own direction strikes out;
  `candidates` lists the pool, and a guard hired there with `hire <candidate> 49 22` on a tick whose low two bits are
  not nought walks, meets the dead end and takes the patrol roll (`q380/confirm.py`). From that cell no try routes: the
  log says `Staff N: thought 0x16` and the frame shows the "?" over him on the queue's fence.

- **The park gate and the ticket booths in both games** (Q471): camera (475,155) 110 180 (`lib/bothcam.py`) shows both.
  The original's 1024 x 768 pixels: booth (47,13) (520,280), gate (47,17) (500,520), its sides (46,17) (580,500) and
  (49,17) (320,540), past them (45,17) (680,520) and (50,17) (300,520); this game's 1280 x 720 at the mapped pixels
  (648,262), (629,488), (704,469), (460,506), (798,488), (441,488), each checked by `pick X Y` first. The original's
  picker answers row 17 over the arch's face, where this game's picks the ground behind it, so take a gate cell's pixel
  at the arch's foot (`q471/confirm.py`'s `find()`; `q471/orig/run.sh` reads the category, the cell and `[0x007ca210]`,
  the entry price screen).
- **What the original blends into the ground as a park loads** (Q617, `q617/orig/run.sh`): `brk.sh 0x0057af4b 200 90 8`
  as `loadfile.sh`'s `POLL`, with `BRK_PRINT` reading the cache's tile size `*(int*)($ecx+4)`, the cell `$esp+4`,
  `$esp+8` and the record at `*(int*)($esp+12)` (`+4` width, `+8` height, `+12` bytes a texel, `+16` the word): one hit
  a blended floor or queue cell, 44 in the stock park, every one before the first frame. Leave HITS above the count:
  the poller ends with the original, so a read before `loadfile.sh stop` is a part. This game's `cell X Y` then says
  what stands on each (`q617/cells.py`, off-screen)

- **The original's picture with one cause held off** (Q479, `q479/orig/run.sh`, `runc.sh`, `rund.sh`): park the pointer
  inside the frame (880,560 on `:77`), never past 0.95 of it, or the camera pans between shots. The fog:
  `memhold.py %PID% 5 0x008bd490:f=10000 0x008bd494:f=20000` (the park view takes none, so nothing changes). The
  compositor's tiles: `memhold.py %PID% 6 0x008bd550=<the word less 4>`. Land outside the park at full light:
  `HF=engine groundcell.py %PID% 6 X0,Y0:X1,Y1=-0x400` (not restored: stop the original after). This game's park
  draws no fog since Q481; the lobby's is taken off in a copy of the bin folder with `fogFactor = 0.0;` in its
  `content/shaders/test.shader` and `water.shader` (the shaders are compiled as it starts), `~/.cache/dn-tmp/q479/nofog`.

- **First person's fog held, in both games** (Q485, `q485/orig/run-b.sh`, `q485/confirm.py`): in the original, first person
  at the stand above and the clock held, `memhold.py %PID% 6 0x008bd490:f=S 0x008bd494:f=E` (the band in clip z, about
  0.707 of the eye depth); here `fog S E`, and `fog file` puts the detail file's band back (`fog` alone is the census).
  Take the ground's frames with `sprites 0` and `shadows 0`, or a guest stands on the sampled line (Q485's R3, twice);
  `q485/linefit.py RUN K...` then reads the fraction along ground lines in both games beside the formula. -2/-1 turns
  every fogged pixel the fog colour, the one test of what a band reaches.

- **Where a ground pattern stands in both games** (Q488): the fence along camera A's top cannot tell a camera a cell off
  from none (its posts stand about one a cell; `q488/fenceshift.py` 0.82 at no shift, 0.78 at one post). Ask each game
  which cell stands under the same pixels instead: the original's `camera.py PID --aim X,Y` for a dozen cells straight
  after `setcam.py` (`q488/orig/run.sh`), then this game's `pick X Y` at the mapped pixels (`q488/ours.py` with
  `PICKS='X,Y;...'`): both named the same five cells. `lib/cellmatch.py` then says which tile is drawn there.
- **A texture of ours made from bytes for `test.shader`** (Q488): the shader turns every v over (`1.0 - y`), so a
  texture laid top down is drawn mirrored about its middle row; lay its rows bottom up (`GroundTiles.Lay`).
- **The original's path tiles, caught** (Q489, `q489/orig/run.sh`): the park opens with the paths in view and their
  tiles cached, so a pan catches none. Start `tilecap.sh` with the camera over them, then set the stale bit once:
  `HF=engine groundcell.py %PID% 0.3 X0,Y0:X1,Y1=+0x100` (`READ=X0,Y0:X1,Y1` with 0.1 s prints the words first); every
  cell of the box is composited again on the next frame, each path cell with its record. A texture the original holds
  in a 16-bit surface is `TextureFile`'s rows bottom up, each channel's top four bits (`q489/layout.py`, `conv3.py`).
- **Save Game from OpenTPW's own menu, by real keys** (Q241j, `q241j/confirm.py`): after `parkrun.py`, `key("Escape")` opens
  the menu (a held tool takes a first Escape), `place("2")` is its Save row; the box opens on "New Save" selected, so
  the name typed replaces it, one `key()` a character, a capital with `mods=("Shift_L",)`, a space as `"space"`;
  `key("Return")` is OK. A row of the list, at the row height its `Save Park: N saved parks ..., rows of R units` line
  gives, puts its name in the box; Enter then opens the question, whose tick is control `9873c9`. The save logs
  `Save Park: '<name>' is written to <path>`; the `windows` census then reads `ParkGadget, ParkViewfinder` (the HUD's
  own), never "none". The `guests` census lists every person: its `model 1` lines are the guests.
- **`wadcat --cat` is for text**: a binary entry comes out with its bytes turned to `?`. Take a binary with `--dump`
  (`wadcat --dump base.MD2 terrain.wad` writes it to the working folder; `q488/wordcmp.py` reads its cell words).
- **A pick compared with the original's own ray** (Q494, `q494/ray.py`): the two cameras at one view (`lib/bothcam.py`)
  cast rays about 0.3 units apart at a model 100 out, which moves a silhouette pixel or a cell edge to the next cell. To
  compare what the two pickers make of ONE ray, read the original's eye (`setcam.py` prints it) and its P per pixel
  (`hoverscan.py ... pick`), then hand ours `pickray EX EY EZ PX PY PZ` in our axes (its x, z, height): the answer's
  world point should be P. `q494/walk.py` replays the original's face walk on a model file with those rays, to tell a
  face-order question from a geometry one; the model's live vertices and normals are read beside it
  (`q494/orig/a-gateverts.txt`, `a-gatenormals.txt`: a model record is `[0x007a4610 + handle * 4]`, its model header
  `[[record + 8] + 4]`, the mesh table `+0x70`, a mesh's vertices `+0x60` in fours of x, y, z, its normals `+0x64`).
- **The original's sea, still or gone** (Q486, `q486/orig/run.sh`): `memhold.py %PID% 5 0x00871f60:f=1.0` holds the
  wave's phase (the quilt stops; it still creeps 0.4 a frame between writes, under one level); `0x00870ecc:f=-50` sinks
  the quilt under the lake, leaving `Line01` alone, OpenTPW's picture. The period of a moving patch: film it
  (`record.sh`), then the box's autocorrelation over the frames, not an FFT of a few seconds, whose bins are too coarse.

- **A person's shadow in both games, measured** (Q480, `q480/orig/run.sh`, `q480/confirm.py`): Mike under the mechanics'
  locator at zoom 110. **Fold the locator (Escape) before reading a shadow's colour**: in the original its patrol squares
  are drawn over the shadow and change what it does to the ground (0.556, 0.716, 0.835 there against 0.6 on bare
  ground); here they write depth over it and hide it. The original's shadows alone go with the render word held with
  `0x20000` (`memhold.py`, under the held clock), which also empties the light list `[0x008c1818]`; ours with
  `shadows 0`. Then `lib/shadowdiff.py` on the pair, `--near` on the person's place. The original's frame is 16-bit: a
  dark channel's ratio there is pulled down by the level kept (blue at 4 to 7 of 31 read 0.543), so judge on red and green.
- **The help bar's lettering in both games** (Q484, `q484/confirm.py`, `q484/orig/run.sh`): ours prints the lines as
  last drawn with the console's `helpbar` ("wrap 351, down Centre, lines 2: ..."; the wrap width is in the font's
  pixels); the original's lines are read off its frames only, at its standing 800 x 600 (font set 2, the set ours draws
  at 1280 x 720, so the breaks compare). Over the gate (475,155) 110 180 and the Belly Bounce (480,265) 150 180 both
  rows wrap; a land row on the buy screen holds a break of its own. Measure the white ink inside the strip's band, and
  keep the gadget out of the band (its white triangle sits at x ~420 on our frame).
- **A karts' or water ride's loop laid by clicks in the original** (Q581, `q581/orig/`, off-screen on `:77` through
  `loadfile.sh`): on `q581/karts.TPWS` (Dino Karts and Splish Splash researched, the bank 60,000) the buy screen
  (113, 590) lists Dino Karts at y 277 and Splish Splash at y 341 (`helpbar.py`'s `buy item` while moving down x 620).
  Picked up, the tool is 4 and the preview's cell `[0x008186b8]`, `[0x008186b4]` follows, so `camto.py` then
  `cellfind.py` find a cell; in mode 0 the preview stands still, so find a cell with `PICK=1` (the picker's) or arm a
  tool first. Put down, it arms the queue (3), not the track: Escape leaves it. The track tool is armed by clicking a
  mouth piece ("Left-click to edit this track"); then each click is a straight run, and `cellfind.py` reaches only
  cells on the anchor's row or column (the preview snaps there), so map the next corner after each click. A
  Splish Splash on (32,15) was refused, its red footprint on the park's fence (`D-2-aim.png`). The purchase leaves a path cell
  before the exit, and a queue run from the entrance ends at once on it: extend that path to the park's (click it,
  "Left-click to extend this path", then the far cell), then click the queue's cell and that path cell to edit the
  queue, which opens the closed, looped ride. A click on a person opens their window: check the mode after each
  click and close a window with its tick (815, 380) before going on. `trackride.py` reads it all.
- **A chosen advisor line in both games** (Q596, `q596/orig/runfs.sh`, `q596/confirm.py`): only a park whose advisor
  talks will do; the stock park, loaded in either game, holds message 128 at score 20 alone and says nothing. A fresh
  Full Simulation park says lines on the same ticks in both, 265, 612, 673, 734, 1277, 1897 and 2517 (responses 404,
  541, 542, 182, 183, 184, 182): `freshfs.sh make` with `POLL` = `advisorline.py` for the original, then `sayas.sh`;
  `lib/freshpark.py` here, `enter()`, then `sayas N` before each line. `sayas.sh`'s lines come through gdb's pipe late,
  so time frames from `advisorline.py`'s `voiceDue=0` instead. Start such a run with the tool's own background mode,
  never with `&` inside a foreground call: there it was ended with the call's limit about 600 s on, the original too.
- **A polled advisor row's score and its inputs, read in the original** (Q602, `q602/run.sh`): `fsload.sh` with a `POLL`
  armed on its second call that starts `advisor.py ... poll`, `advisorline.py` and `WORDS=2 ./brk.sh A,B,C,D 8 70 2` together,
  A to D the score function's compares (the instruction after each input's `CALL`, and the one after the last `AND`): each
  hit's `eax` is that input on the poll's tick. Where the row is the poll's answer each comes twice, the poll and the tick's
  rescore; a row scoring nought or refused is hit once, and a failed test ends the hits early (returns `0x0059d1c2`,
  `0x0059d1e7`, `0x0059d206`). Predict the inputs
  from the file first (`parkfile.py`); four hardware breakpoints is `gdb`'s most.
- **An Alt chord in both games** (Q545, `q545/orig/run.sh`, `q545/confirm.py`): the original's on `:77` is `xdotool
  keydown Alt_L`, the letter down and up, then `keyup Alt_L`; ours is `key( "s", what, mods=("Alt_L",) )` through
  `parkrun.py`. Both act on the letter's release, so stamp a reading straight after its key-up, not before Alt goes
  down: there the original's clock had run 3 ticks on (run a's miss). The quicksave the original writes, `jungle.TPWS`,
  must be taken out after (the script does it).
- **The gadget's date at a known tick in both games** (Q149): here `enter()`, `pause`, `tick()` (the console's `sweeps`) and a `shot()`, the gadget at (20,390)-(230,710) of the 1280 x 720 window; each turn of the calendar is logged `Calendar: <date> on mGameTick <T>, the day's change[, the month's][, the year's]`. The original: `original/gadgetdate.sh` with the ticks wanted. The date of tick T is 2000-01-01 plus T x 3,750 s; ours letters it M/D/YYYY, the original M.D.YYYY (Q527).

## The original under Proton

The original game (`testme.exe`, the 2.0 build Ghidra holds) runs on this machine through GE-Proton, as a
reference instrument (Q168, `docs/history/queue-done.md`). It is Linux only: Proton is Wine for Linux. It is not an emulator and it
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
| `original.sh start` | Starts the reference install on the real screen, silent (null ALSA), capped at 30 fps (`ORIG_FPS=60` caps it at 60; warning 1), with the Proton log and a frame-rate log |
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
| `watch.sh 'EXPR' SIZE [HITS] [SECS]` | Which code writes a piece of memory: a hardware watchpoint through `gdb`, attached and detached again. `WATCH=rwatch` asks who READS it instead (`awatch` both): a silence is evidence only beside a control that fires, `mGameTick` (`'*(unsigned*)0x80239c + 0x1da70c'`) is read at once (Q361) |

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

**Resolution** (Q377). `Data/Resolution.sam` in the reference install sets the game's own resolution (1 512 x 384 to
7 2048 x 1536; absent, the Options screen's, which is 640 x 480 for the reference player); `resolution.sh` writes it.
**It stands at 800 x 600**, Res 3, from 2026-10-15: font set 2 (`[0x0078b534]`), the set this game draws with at the
harness's 1280 x 720 window, so lettering and every font-measured size (a list's rows: 39 units in both) compare like
with like. Measured at 640, 800 and 1024 (`q377/orig`): the clock TRUE and 30 fps at 1024 off-screen; `loadfile.sh`,
`gmove.py` hovers and clicks, a right click and `screenpark.sh` (after its 3 s fix) all work at each; the frames stay
1024 x 768 off-screen and the 3D view's framing is the same (the gate's box at the file's camera, 1024 against 640:
383 pixels differ, a running park's own). **What differs**: the fonts (640: set 1; 800: 2; 1024: 3, so lettering and
a list's rows are larger or smaller than this game's), the frame's sharpness (640 and 800 are scaled up to
1024 x 768, which blurs a glyph's edges: `listcells.ink` reads a band's height only on a native frame), and at 1024 the
game's cursor is drawn MAGENTA. Take `resolution.sh 1024` for a native, sharp frame (a bar's edge, a texture), and
put it back to 800 after. A number of the original's measured before 2026-10-15 that rests on its fonts is the
640 x 480 one's.

### Warnings, most important first

1. **The park clock follows the computer's uptime, so `original.sh` starts the game in a fresh-boot clock.** The
   park clock is a double of "milliseconds since boot", added to each frame at 24-bit precision (`docs/exe/park-engine.md`,
   "The park clock loses precision with uptime"). Measured at 6.5 days of uptime: uncapped, the park froze (0 ticks in
   30 s); at 30 fps it ran 1.38 to 1.54 times real time; at about 4.5 fps it ran at real time. Since 2026-10-20
   `original.sh start` runs the game in a time namespace that reads 60 s since boot (`timens.py`, above): at 8.5 days of
   uptime, started plainly it ran 1.44 times real time (46.5 ticks a second, its clock 732,223,744 ms); in the namespace
   1.00 (32.2 ticks a second, its clock 215,367 ms), `tpwmem.py clock` TRUE (`timens-clock/PREDICTION.txt`). No
   restart is needed. **Still run `tpwmem.py clock` before any timing, and discard timings it calls NOT TRUE**
   (`VERIFYING.md` rule 127): `original.sh` says when the namespace could not be made and starts the game plainly. The
   30 fps cap stays on; uncapped in the namespace is not tried. `ORIG_FPS=60 original.sh start` caps it at 60 instead
   (a copy of `play.sh` in `logs/`): Wine's `+fps` read 59.6 to 60.3 and `tpwmem.py clock` TRUE (Q642).
2. **Never point it at `~/Games/TPWorld`.** The original writes `save/`, `Config.tcf`, `enginedebug.txt` and
   `fallback.txt` into its own folder. The reference install is its own copy.
3. **On the real screen it takes the whole display and the mouse.** Start it only when the other session is idle;
   the run needs no question first (Alexah's standing word). Your monitor's mode is not changed: Proton scales the
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
    its clock never runs. Seen at 6.9 days of uptime, and at 8.1 (2026-10-20, Q558) loaded by a fresh Full Simulation
    player, off-screen through llvmpipe and through zink alike: frozen on tick 19007, the frame unchanged for 150 s. **Under
    the fresh-boot clock** `original.sh` gives since 2026-10-20 (warning 1) the same park, loaded by `fsload.sh`, ran past
    tick 19100 and its frames moved for 80 s (Q591, `q706/orig/O`): one run, so the clock is the likely cure, not a proven one.

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
`tqi/` folder, with their own venv (`CLAUDE.local.md`); a three-frame run takes under a second. Those venvs were made inside the VS Code sandbox (Python
3.13) and do not run outside it (3.12): `~/.cache/tpw-emu312` (unicorn, pefile, numpy, Pillow) does, and `lib/lndtile.py` uses it.

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
