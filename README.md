<p align="center">
    <h1 align="center">
        OpenTPW
    </h1>
    <p align="center">
        An open-source re-implementation of <a href="https://en.wikipedia.org/wiki/Theme_Park_World">Sim Theme Park / Theme Park World</a>.
        <br>
        <a href="https://opentpw.gu3.me/formats/">Format documentation</a>
    </p>
</p>

![The lobby looking out on Halloween World: the park gates and their name board, the tree with the carved face, a bolt of lightning over the island, and the advisor on screen](.github/screenshot.png)

## What this is

Theme Park World (1999) is hard to run on a modern machine. OpenTPW re-implements the game's engine on top of your own copy of the original, reading its real data files - models, textures, sounds, fonts and saves - and drawing them with Vulkan.

**You need a legal copy of the original game.** OpenTPW ships no game content; it is an engine, not a download.

**It is not playable yet, but a park now runs.** You can explore the lobby and the front end - pick a park, look around the islands, hear the advisor - and enter a park and look around it, ground and scenery and the shops and rides it was laid out with. A park *runs*: its clock ticks, its weather turns, guests arrive by bus, seaplane and ferry, walk about, pay at the gate, queue, ride, buy from the shop and the sideshow, and go home again when their day runs out. Things can be bought, sold, moved and hired, and paths and queues can be laid and lifted. It has the management gadget the original puts in its corner, with a live date on it, and **five of that gadget's six buttons open a real screen** - only Research does not, and that is because this game has no research to put behind it. What is still missing is a park you can *finish*: no finances, no litter, and no way to save one. See [Status](#status) for the full picture.

## Quick start

1. Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).
2. Copy your Theme Park World disc to a folder you can write to (see [Getting the game's files](#getting-the-games-files) - *how* you copy it matters).
3. Build and run:

```sh
dotnet build source/OpenTPW.sln
dotnet source/OpenTPW/bin/Debug/net10.0/OpenTPW.dll --game "/path/to/Theme Park World"
```

That is the whole setup. SDL, SPIRV-Cross and shaderc travel with the build, and OpenTPW uses the copies it shipped rather than anything installed on your machine.

The one thing you may still need is a **Vulkan driver**, which OpenTPW cannot ship for you. On Windows and macOS your GPU driver already provides it. On Linux, install your distribution's Vulkan loader - packaged as `libvulkan1`, `vulkan-loader` or `vulkan-icd-loader` depending on the distribution - plus Mesa or your vendor's driver.

## Getting the game's files

**Copy the disc keeping its long file names.** This is the single most common way to end up with a copy that cannot work. A CD carries two directory trees, and the plain ISO 9660 one is uppercase and cut to 8.3 - `CHALLE~0.SAM` where the game asks for `Challenges.sam`. Those are different *names*, not different spellings, so no amount of case-matching can repair them. Mount the disc and copy from the mount, or use a tool that preserves the Joliet names. OpenTPW detects a short-name copy and tells you, rather than failing mysteriously later.

A folder counts as the game when it contains a `data` folder with the game's `levels` inside it. Capitalisation never matters - the disc spells it `Data`, an installed copy spells it `data`.

**Where to put OpenTPW.** The tidiest arrangement is to put the build in with the game's files, where `TP.exe` was. That is how the original located its own data, and it is what OpenTPW is built around. Otherwise just point it at the folder:

```sh
OpenTPW --game "/path/to/Theme Park World"
OPENTPW_GAME_PATH="/path/to/Theme Park World" OpenTPW
```

Quote the path - the folder the original installs into has spaces in its name.

If you tell it nothing, it searches in this order: `--game` on the command line, `OPENTPW_GAME_PATH`, the `GamePath` setting if you have changed it, the folder the build sits in, the working directory, and finally the default Windows install location. Each of those is a single folder - it does not climb up through parent directories hunting for a copy of the game. If none of them holds it, OpenTPW stops and lists everywhere it looked.

Saves are written to `save/` beside the game's data, exactly where the original keeps them, so a park saved by one is visible to the other.

## Controls

The game tells you about none of these:

| Key | Does |
|-----|------|
| `Escape` | Game menu - or, standing on the ground in a park, back to the orbit camera first |
| `Ctrl`+`H` | Show or hide the help bar |
| `F2` | Hide the interface |
| `X` | Freeze the lobby camera |
| `[` `]` | Move between islands |
| `C` | In a park: stand on the ground and look around, and back again |
| `←` `→` | Turn the park camera |
| Mouse wheel | Zoom the park camera in and out |
| Left-click | The park gadget's buttons, and the eject button while standing on the ground |

## Platforms

Vulkan is the only backend, on every platform - so a bug report from Windows lands on the same code Linux runs every day. macOS reaches Vulkan through MoltenVK, which ships with the build.

| Platform | Builds | Run |
|----------|--------|-----|
| Linux x64 | ✅ | ✅ Developed and tested here |
| Linux arm64 | ✅ | ❔ Never run |
| Windows x64, arm64 | ✅ | ❔ Not run recently |
| macOS Intel, Apple Silicon | ✅ | ❔ Never run |

OpenTPW compiles for all seven runtime identifiers - `linux-x64`, `linux-arm64`, `linux-arm`, `win-x64`, `win-arm64`, `osx-x64`, `osx-arm64` - and each build receives its complete set of native libraries, MoltenVK included on macOS.

**But building is not running.** Only Linux x64 is actually exercised; nobody on the project has a Mac or an arm64 machine. The other platforms are written to be correct and are believed to work, and bugs there will be found by whoever reports them first. Please do report them.

### Wayland

The SDL that ships with OpenTPW is built for X11 and KMSDRM, with no Wayland backend, so a Wayland session runs through XWayland. That works, but it scales and handles input differently from a native Wayland window - and a session without XWayland installed will not start at all.

If your distribution's SDL2 is better, use it instead:

```sh
OPENTPW_SYSTEM_SDL=1 dotnet source/OpenTPW/bin/Debug/net10.0/OpenTPW.dll --game "/path/to/Theme Park World"
```

That substitutes SDL only. SPIRV-Cross and shaderc still come from the build, because those are not libraries a machine usually has.

## Status

**What works.** The lobby: four islands with their gates, flyers, sky, ocean, weather with thunder and lightning, and the park name signs. The original front end, drawn with the game's own interface meshes and `.bf4` fonts - player slots, the new player dialog, the quit box, the island panel with its golden key prices, and the help bar. The advisor, with lip sync, queued lines and interruption. The original particle system and its on-screen effects. The Escape menu and options screen, with volumes that apply as you drag them, working display modes and resolutions, and a window you can resize to any aspect ratio. Machine options and players save to `save\Config.tcf` and `save\users`, in the original's own formats.

**And a park.** Choosing a park in the front end enters it. Its land is built from the heightfield inside the theme's `base.MD2` and drawn with the park's own ground textures, each cell laid the way its flags say; the attribute map beside it is read, so the engine knows what every cell *is*. The theme's fixed scenery loads, and so do the fixed items the save never gives a position to - the entrance gate, with its doors animating and the park's name painted onto its board, and the traffic lights on both pedestrian crossings. The paths the park was laid out with are drawn as well - straights, corners, T-junctions, a crossroads and the edged sides of the double-wide avenue running up from the gate - each cell taking the tile and the quarter turn its save file records, because a path stores both rather than leaving them to be worked out from its neighbours. The queues are drawn as well, and they turn out not to be paths at all: a queue is built from small railed models the theme keeps in a `queue.wad` of its own, one to a cell, and each cell names which piece it is - an end where the queue meets the path, straights along its length, and a bend where it turns in towards the ride.

**And the things it was laid out with.** The park's own save file is walked, and the eleven objects Lost Kingdom was built with stand on the cells it gives them: a drinks shop, a ride, a sideshow, three toilets, a staff room, two security cameras, a litter bin and a fountain that runs with water. Each takes the art it does not ship itself from the theme's shared texture archive, which is how the original arranges it - without that, most of every item's surfaces have no texture to draw. Each also stands as the thing it was built into rather than the way it first arrived: an item is put up by a one-shot clip, and the last frame of that clip is what a finished one looks like, which is why the ride is a hatched dinosaur rather than an egg with a dinosaur drawn through it. Each stands on its own floor, too. Every item's model opens with a flat plate exactly as wide as its footprint, and the cells a built thing covers are now left out of the ground, so that floor is what shows instead of grass drawn over the top of it. A thing its park saved turned stands on the cells that park marks for it as well, which is what puts the fountain and the staff room against the path rather than a couple of cells adrift of it.

**And a way out of it.** Escape brings up the park's own game menu. It is the same widget the lobby's Escape opens - one menu list, built twice - carrying the choices a park has rather than the lobby's, in the order the original adds them: Load, Save, Restart Park, Publish Park, Options, Resume Game, Exit To Lobby and Quit Game. Exit To Lobby ends the park and builds the lobby again, and asks nothing first, which is how the original leaves one; Restart Park and Quit Game both ask in a message box, as they do there. Go Offline is missing because the original only adds it while you are connected to an online world that this has no code to reach. Load, Save and Publish Park are shown because the original shows them, and each says plainly that it cannot do it yet rather than appearing to work: nothing writes a park back, and there is nowhere to publish one to.

**And a menu that stops the park.** Opening that menu, a message box or the options screen now holds the world: the game's clock stops, and the animations stop with it, so a park's picture holds still until you close it again. That is how the original does it - it freezes one clock, and the thirty-one millisecond tick loop that drives the world then simply finds nothing to do, so nothing has to know it has been paused. It is also why the lobby does not stop: the original's pause only acts while a park is running, and the lobby holds its advisor instead, which is what it already did here. The interface keeps its own time either way, so the menu you are reading stays alive over a frozen park.

**And its own sky, and its own music.** A park is drawn under the sky its own theme ships rather than the lobby's: every level carries a `sky` folder holding the same three textures, and the original hands them to one shared loader from both scenes. It stands where the original leaves it - centred on the world origin, three hundred units up - and it is deliberately left untinted, because the colour the lobby floods into its nearest cloud layer comes from an island script that a park has no equivalent of; a park's sky takes its colours from its own artwork instead. **You will not see it from the park camera**, whose angles never rise above the horizon - and neither does the original, whose own screenshots show ground to every edge. It is there for the first-person view - which now exists, so the sky a park loads is finally something you can look at. The park also plays its theme's music, picked from the arrangements that theme ships - a hundred and twenty-nine of them in Lost Kingdom. The original swells that with the size of the crowd, and so plays it silently in an empty park; with nobody to count yet, this plays it at a fixed level measured against the same target the lobby's music is set to.

**And weather.** A park's weather turns on its own. Everything follows from one number - a quality from 1 to 100 that the game reads backwards, so a low one is bad weather: below forty it rains, and below fifteen it throws lightning as well. That quality is rolled from the season's own average and re-rolled every seven game days, and a game day here is about five and three quarter seconds, so a park's weather changes roughly every forty seconds of real time. It is settled three days before it arrives, which is what the original's days-of-warning setting is for. Rain builds and fades rather than switching on and off, twenty drops at a time, four times a second - and the heaviest rain the game can make is five hundred and eighty drops rather than the six hundred its own cap allows, because a quality can never quite reach zero. Lightning comes down anywhere across the park, and its thunder follows a beat or two later and sounds from where the bolt fell, closing in as a storm builds: the first flash is a couple of seconds ahead of its thunder and later ones follow almost at once. **You will mostly not see the bolts from the park camera**, and for the same reason you cannot see the sky from it - they stand three hundred units tall while that camera never looks above the horizon, so what reaches the frame is a bright diagonal near the ground. From the ground, they are the whole height of the sky. The weather is per-theme, which is worth saying because three of the four read the same numbers and Halloween World does not: it rains harder there, throws more lightning, and is the only theme in the game that can snow. Snow is built in the original and its data really does reach it; it is not drawn here, because Halloween World is the one theme with no saved park to enter.

**And a way to stand in it.** Press `C` in a park and the camera comes down to head height, five units above whatever the ground is doing, and the park is yours to look around: the view follows where the pointer sits rather than how far it moved, and the walk keys carry you across the land with the eye riding it, so a ridge lifts you rather than stepping you. Press `C` again and the orbit camera takes back over, looking at wherever you walked to. The original calls this camcorder mode and reaches it two ways - this key, and the camera button on the park's management gadget, which now works too. Leaving it has three roads, as the original has: the key again, `Escape`, and the eject button in the corner of the viewfinder. It is also the only way to see a park's sky and the full height of its lightning.

**And a corner to manage it from.** A park wears the management gadget the original puts at its bottom left, read out of the game's own compiled layout data rather than measured off a screenshot: the visitors' happiness gauge, the date - which is live, and turns a day about every five and three-quarter seconds of real time - and the six round buttons, each with the game's own description of it on the help bar. The gauge now moves with the mean happiness of the park's guests, and four of the six buttons have nothing to open, so they say why rather than pretending: no screen in this game buys an attraction, hires anyone, or shows the finances. The two that work are the camera and the park map. Step down to the ground and the gadget puts itself away, and the camcorder's viewfinder takes the screen instead - the frame, and the eject button that leaves - which is what the original does, and what stops a management panel being swept past while the view turns toward wherever you point.

**And the people it was saved with.** The thirteen guests and five staff the save was holding are read out of it, drawn as the sprites they wear, and take their turns on the engine's own schedule. A guest walks the route the save gave them, forms an opinion of the admission fee, and either pays it or sets off for the bus stop. Past that point it is patchy: a guest who pays stands at the booth, because the gate's own one-guest-at-a-time handshake is not built, and a guest who reaches a queue stands in that instead. The staff run one shared machine with five extensions: the guard and the researcher patrol beats of their own, and a worn-out one of any kind finds the park's rest area and walks to the cell it is approached from.

**And rides that run their own scripts.** Every placed thing runs its own compiled script - 74 of the 106 instructions the opcode table declares, with the rest counted rather than guessed at - and a ride takes its turn from the same sweep the people take theirs from, inviting whoever is at the front of its queue. This once said only two of Lost Kingdom's things could be offered to a guest at all - the sideshow and the ride - because the others "declare no queue cells, and an object with no queue cells has no room". **That reading was refuted.** The field the offer filter tests is `+0x40`, and the engine **overwrites** it by walking the real queue cells off the map before the count is ever read, so the save's copy is a *cache* and not a declaration. Guests choose, queue for and buy from the Drinks Shop too. The argument that should have raised the doubt years earlier is that the three toilets sit in the identical position: under the old reading no toilet in any park could ever have been visited.

**And they do ride.** The boarding chain runs end to end - a guest queues, boards, rides, gets off and is charged - and the money lands: a filled park took **1110 at the Drinks Shop** (37 sales at 30) and **900 at the Jungle Spray** (45 at 20), with guests seen reaching the riding states and the Belly Bounce's queue holding 2 of its 16 places. **This paragraph has been wrong in both directions**: it once claimed they rode on the strength of a test harness, was corrected to say nobody did, and is now true on the strength of four runs of the real park confirmed on screen and by census.

**What does not.** There are **no finances**: the two global income pools, the staff wage bill as a running cost, and every figure a park's accounts would need are named and unbuilt, so money moves but nothing balances. The handyman, the mechanic and the entertainer finish the walk the save left them on and stand, because their work wants litter, breakdowns and an audience: **this park holds no litter at all in any of its 16,384 cells**, so a working handyman would correctly find nothing. The gadget draws, its date runs, and five of its six buttons open a real screen - but the **message bar is deliberately absent**, because nothing in this game can put a message in it, and **Research** has no screen because this game has no research, no researchers and no groups to put behind one. **Nothing writes a park back** - and no saved park has ever been read by anything but this one file, so the reader is not assumed to generalise. Eight of the nine per-object windows are unbuilt. There is no video, and nothing online: there is no networking code in the project at all.

## File formats

- ❌ Not implemented &nbsp;&nbsp; ⚠️ Partially implemented &nbsp;&nbsp; ✅ Implemented

| Format                                                  | Status |
|---------------------------------------------------------|--------|
| Archives (.WAD, .SDT)                                   | ✅     |
| Textures ([.WCT](https://opentpw.gu3.me/formats/wct.html))                    | ✅     |
| Settings ([.SAM](https://opentpw.gu3.me/formats/sam.html))                    | ✅     |
| Sounds ([.SDT](https://opentpw.gu3.me/formats/sdt.html), .MP2) \*\*            | ✅     |
| Strings ([.BFMU](https://opentpw.gu3.me/formats/bfmu.html), [.BFST](https://opentpw.gu3.me/formats/bfst.html)) | ✅     |
| Fonts (.BF4)                                            | ✅     |
| Lip Sync ([.LIP](https://opentpw.gu3.me/formats/lips.html))                   | ✅     |
| Particles (.PLB, .ESP, .TPC)                            | ✅     |
| Machine and player saves (Config.tcf, gms.dat)          | ✅     |
| Map Data ([.MAP](https://opentpw.gu3.me/formats/map.html))                    | ✅     |
| Texture tables (.TCT)                                   | ✅     |
| Models ([.MD2](https://opentpw.gu3.me/formats/m3d2.html)) \*                   | ⚠️     |
| Sound categories (cat_\*.map) \*\*                                             | ⚠️     |
| Park signs (.SGN) \*\*\*                                                        | ⚠️     |
| Park saves ([.TPWS](https://opentpw.gu3.me/formats/tpws-ints-lays.html)) \*\*\*\*      | ⚠️     |
| Ride Scripts ([.RSE](https://opentpw.gu3.me/formats/rsse.html)) \*\*\*\*\*             | ⚠️     |
| Materials ([.MTR](https://opentpw.gu3.me/formats/mtr.html))                   | ❌     |
| Video ([.TQI](https://opentpw.gu3.me/formats/tqi.html))                       | ❌     |

<details>
<summary><b>The detail behind the partial ones</b></summary>

\* **Models (.MD2)**: static mesh geometry (verts/faces/materials) loads reliably, along with the node tree that places the meshes and the ids a character's costume pieces are found by. The same extension is also used for a structurally distinct keyframe animation format, of which five channel kinds are decoded: per-vertex morph animation (768 files), per-node quaternion rotation (686 files), UV scrolling (324 files), position (455 files) and visibility (404 files), counted by the channels each file's tracks declare. 1183 of the game's 1279 animation files carry at least one of them; 6 carry only channel kinds that aren't decoded yet and 90 contain no animation data at all. The lobby plays morph, rotation and UV; the advisor plays position and visibility; and a park reads the visibility channel to leave an item looking the way its construction ends it. A rotation key is the orientation a node holds *inside its parent* rather than the one it ends up with in the model - the two are the same only under a root that does not turn, which every gate in the game happens to be, and they differ on 1,078 of the game's 2,592 rotation tracks.

\*\* **Sounds (.SDT, .MP2, cat_\*.map)**: banks are read, and their audio decodes and plays. Despite the .mp2 extension on every name inside a bank, the audio is not always MPEG Layer II - 2,646 of the game's 3,739 streams are Layer I - so both layers are decoded, and Layer III does not occur. Nothing in the game addresses a sound by file name: sounds are grouped into categories and code plays a numbered effect within one, which is what the cat_\*.map pair holds. Its bank half is fully decoded. Of its effect half, the header, the effect table and the sample records are decoded, but the variable-size header in front of each effect's sample list is not, so the records are located by validating them against the banks rather than by offset. How those records divide between the effects is read rather than guessed at, from a field in the effect table saying how many weighted lists each effect picks between - checked against all thirty-one categories the game ships.

\*\*\* **Park signs (.SGN)**: a park's name board renders with the fonts, colours and artwork the file asks for. What sits between the font records and the artwork is two per-line fill textures, each sixteen by a hundred and twenty-eight with its own width, height and bytes-per-pixel in front of it - which is what the ints reading 16, 128, 4 are. The engine resamples one to the height of its line and tiles it across the glyph mask; OpenTPW letters each line in its ink block's flat colour instead, so a sign whose fill is a gradient is drawn slightly plainer than the original draws it. A ride's name board is the same format, and reads: all eighty-four signs the game ships are read, twenty-three carrying artwork and sixty-one bare. The thirty-six bytes that once looked like a second variant are one record - the artwork's own header - which a bare board simply does not carry, so the file has to be walked in order rather than indexed at fixed offsets. A ride letters its own name onto its board, and a bare board is cleared to transparent so the lettering sits on the ride itself.

\*\*\*\* **Park saves (.TPWS)**: the container is read - the header is parsed and its ZLIB payload inflated - and the shipped park's own numbers are pinned by tests, including that its first four bytes are a *version* of 400 rather than the magic number they were once taken for. Inside, the payload is a sequence of seventeen module blocks, and the first and largest of them, the world, is now walked: its header, its 16,384 map cells and its list of forty-two things, which is where the objects a park places are found. The walk is checked the way the original checks it, by having to end exactly on the next module's tag - and each of the 16,384 cells is measured individually, so all of them landing on the next cell's first byte is a sharper check than the tag alone, which a pair of compensating errors would still reach. The cells are read rather than merely stepped over: each says what is built on it, which of its neighbours it joins and which way it faces, and - where the player laid a path - which tile that cell draws and which quarter turn the tile takes, which is where a park's walkways come from, since they are in neither the ground model nor the attribute map. What else the walk reads out is what each object *is* and where it stands; the litter and pylon bookkeeping inside each cell, and the other sixteen modules, are stepped over rather than understood, and nothing writes a park back. The saves that do work in full are the machine's options and the players themselves, listed separately above.

\*\*\*\*\* **Ride Scripts (.RSE)**: scripts are read and they run. The container is parsed - header, tag bytes, branch targets and the variable-name tail - and every ride script the game ships both reads and runs, which is what pins the parse: a branch that landed anywhere but the start of an instruction, or a string operand naming a string that is not there, would fail on some script somewhere. The runtime carries 74 of the 106 instructions the opcode table declares; the rest fall to a no-op that is *counted* rather than guessed at, because an instruction implemented wrongly is worse than one left out.

</details>

## Troubleshooting

**"Theme Park World was not found."** OpenTPW lists every folder it looked in. Point it at the game with `--game <folder>` or `OPENTPW_GAME_PATH`, or put the build in with the game's files.

**"its names have been cut short."** The disc was copied from its plain ISO 9660 tree. Copy it again keeping the long names - see [Getting the game's files](#getting-the-games-files).

**It stops before a window appears, saying it could not load a native library.** OpenTPW ships the libraries it opens, so this is almost always the Vulkan loader or the GPU driver, which it does not ship. Install your distribution's Vulkan loader and your GPU's driver.

**On Wayland the window scales oddly, input feels wrong, or SDL says "No available video device".** Install XWayland, or run with `OPENTPW_SYSTEM_SDL=1` - see [Wayland](#wayland).

**No sound.** Not fatal - OpenTPW warns and carries on.

## Building and testing

```sh
dotnet build source/OpenTPW.sln
dotnet test source/OpenTPW.sln
```

It does not matter what directory you start the game from; the shaders and the loading screen's font are copied next to the binary and found there.

Four hundred and seventy of the eight hundred and ninety-six unit tests read real game files and skip when no installation is found - so a green run on a machine that has never had the game means **426 ran and 470 did not**. Set `OPENTPW_GAME_PATH` to run all of them. (Take these fresh; they move most sessions.)

`OPENTPW_DEBUG_CONSOLE=1` reads commands from standard input, for driving a run reproducibly.

## Documentation

File format information lives at the [OpenTPW formats](https://opentpw.gu3.me/formats/) site, whose source is the [OpenTPW.FileFormats](https://github.com/OpenTPW/OpenTPW.FileFormats) repository. It is a work in progress, but aims to be thorough.

## Contributing

Contributions are very welcome:

1. Fork the [original project](https://github.com/OpenTPW/OpenTPW).
2. Create a branch named `YourName/FeatureName`.
3. Open a pull request when you are ready.

## License

MIT - see [LICENSE.md](https://github.com/OpenTPW/OpenTPW/blob/main/LICENSE.md).
