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

**It is not playable yet.** You can explore the lobby and the front end - pick a park, look around the islands, hear the advisor - and you can now enter a park and look around it, ground and scenery and the shops and rides it was laid out with. But a park does not *run*: nothing operates, nobody visits, there is no interface, and no way back out to the lobby short of restarting. See [Status](#status) for the full picture.

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
| `Escape` | Game menu |
| `F1` | Ask the advisor for help |
| `F2` | Hide the interface |
| `X` | Freeze the lobby camera |
| `[` `]` | Move between islands |

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

**And a park.** Choosing a park in the front end enters it. Its land is built from the heightfield inside the theme's `base.MD2` and drawn with the park's own ground textures, each cell laid the way its flags say; the attribute map beside it is read, so the engine knows what every cell *is*. The theme's fixed scenery loads, and so do the fixed items the save never gives a position to - the entrance gate, with its doors animating and the park's name painted onto its board, and the traffic lights on both pedestrian crossings.

**And the things it was laid out with.** The park's own save file is walked, and the eleven objects Lost Kingdom was built with stand on the cells it gives them: a drinks shop, a ride, a sideshow, three toilets, a staff room, two security cameras, a litter bin and a fountain that runs with water. Each takes the art it does not ship itself from the theme's shared texture archive, which is how the original arranges it - without that, most of every item's surfaces have no texture to draw. Each also stands as the thing it was built into rather than the way it first arrived: an item is put up by a one-shot clip, and the last frame of that clip is what a finished one looks like, which is why the ride is a hatched dinosaur rather than an egg with a dinosaur drawn through it.

**What does not.** Leaving a park once you are in one - only the debug console can. The objects a park places now stand in it, but none of them *works*: a ride is scenery, a shop serves nobody, and there are no paths or queues, no staff and no visitors. Nor is there a park interface, ride scripts, video, or anything online - there is no networking code in the project at all.

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
| Models ([.MD2](https://opentpw.gu3.me/formats/m3d2.html)) \*                   | ⚠️     |
| Sound categories (cat_\*.map) \*\*                                             | ⚠️     |
| Park signs (.SGN) \*\*\*                                                        | ⚠️     |
| Park saves ([.TPWS](https://opentpw.gu3.me/formats/tpws-ints-lays.html)) \*\*\*\*      | ⚠️     |
| Ride Scripts ([.RSE](https://opentpw.gu3.me/formats/rsse.html)) \*\*\*\*\*             | ❌     |
| Materials ([.MTR](https://opentpw.gu3.me/formats/mtr.html))                   | ❌     |
| Video ([.TQI](https://opentpw.gu3.me/formats/tqi.html))                       | ❌     |

<details>
<summary><b>The detail behind the partial ones</b></summary>

\* **Models (.MD2)**: static mesh geometry (verts/faces/materials) loads reliably, along with the node tree that places the meshes and the ids a character's costume pieces are found by. The same extension is also used for a structurally distinct keyframe animation format, of which five channel kinds are decoded: per-vertex morph animation (768 files), per-node quaternion rotation (686 files), UV scrolling (324 files), position (455 files) and visibility (404 files), counted by the channels each file's tracks declare. 1183 of the game's 1279 animation files carry at least one of them; 6 carry only channel kinds that aren't decoded yet and 90 contain no animation data at all. The lobby plays morph, rotation and UV; the advisor plays position and visibility; and a park reads the visibility channel to leave an item looking the way its construction ends it. A rotation key is the orientation a node holds *inside its parent* rather than the one it ends up with in the model - the two are the same only under a root that does not turn, which every gate in the game happens to be, and they differ on 1,078 of the game's 2,592 rotation tracks.

\*\* **Sounds (.SDT, .MP2, cat_\*.map)**: banks are read, and their audio decodes and plays. Despite the .mp2 extension on every name inside a bank, the audio is not always MPEG Layer II - 2,646 of the game's 3,739 streams are Layer I - so both layers are decoded, and Layer III does not occur. Nothing in the game addresses a sound by file name: sounds are grouped into categories and code plays a numbered effect within one, which is what the cat_\*.map pair holds. Its bank half is fully decoded. Of its effect half, the header, the effect table and the sample records are decoded, but the variable-size header in front of each effect's sample list is not, so the records are located by validating them against the banks rather than by offset.

\*\*\* **Park signs (.SGN)**: a park's name board renders with the fonts, colours and artwork the file asks for. Two regions of its header - a 64x64 image at 0x03C5 and 36 bytes at 0x03A1 - are not identified. A ride's name board is a second variant of the format and is not read: the layout the gates and lobby islands use runs to 0x43DD before the image begins, and every ride's sign is 36 bytes short of that, so a ride wears the placeholder its artwork ships with.

\*\*\*\* **Park saves (.TPWS)**: the container is read - the header is parsed and its ZLIB payload inflated - and the shipped park's own numbers are pinned by tests, including that its first four bytes are a *version* of 400 rather than the magic number they were once taken for. Inside, the payload is a sequence of seventeen module blocks, and the first and largest of them, the world, is now walked: its header, its 16,384 map cells and its list of forty-two things, which is where the objects a park places are found. The walk is checked the way the original checks it, by having to end exactly on the next module's tag. What it reads out is what each object *is* and where it stands; the rest of that block, and the other sixteen modules, are stepped over rather than understood, and nothing writes a park back. The saves that do work in full are the machine's options and the players themselves, listed separately above.

\*\*\*\*\* **Ride Scripts (.RSE)**: there is no parser. What exists is the opcode scaffolding for the ride virtual machine, against a claimed total of 210 instructions.

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

Thirty-six of the eighty unit tests read real game files and skip when no installation is found - so a green run on a machine that has never had the game means 44 ran and 36 did not. Set `OPENTPW_GAME_PATH` to run all of them.

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
