<p align="center">
    <h1 align="center">
        OpenTPW
    </h1>
    <p align="center">
        An open-source re-implementation of <a href="https://en.wikipedia.org/wiki/Theme_Park_World">Sim Theme Park / Theme Park World</a>.
    </p>
</p>

![The lobby looking out on Halloween World: the park gates and their name board, the tree with the carved face, a bolt of lightning over the island, and the advisor on screen](.github/screenshot.png)

## What this is

Theme Park World (1999) is hard to run on a modern machine. OpenTPW re-implements the game's engine on top of your own copy of the original: it reads the game's real data files - models, textures, sounds, fonts and saves - and draws them with Vulkan.

**You need a legal copy of the original game.** OpenTPW ships no game content. It is an engine, not a download.

## Can I play it yet?

**Not yet.** It is a work in progress, and it is worth knowing what to expect before you try it:

- **The lobby works.** The four islands, the front end, the advisor, weather, the options screen and your saved players.
- **Only Lost Kingdom is partly working, and only from the park the game ships already built.** Enter it and the park runs: guests arrive, queue, ride and buy things; you can buy, sell and move rides, hire staff and lay paths.
- **A fresh, empty park does not work, on any of the four islands.**
- **There is no way to finish or keep a park.** No finances, no litter, and nothing saves a park back.

[`docs/STATUS.md`](docs/STATUS.md) has the full, current list of what works and what does not.

Want to play the original game on Linux in the meantime? [`tools/play-the-original`](tools/play-the-original/README.md)
sets it up with Proton. You need your own disc and your own no-CD `.exe`.

### Which version of the game?

OpenTPW has only been tested with the **American release, Sim Theme Park**. Theme Park World, the release sold elsewhere, uses the same engine and data and **should** work, but nobody has tried it yet. If you do, please report how it goes.

## Quick start

1. Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).
2. Copy your game disc to a folder you can write to. *How* you copy it matters - see [Getting the game's files](#getting-the-games-files).
3. Build and run:

```sh
dotnet build source/OpenTPW.sln
dotnet source/OpenTPW/bin/Debug/net10.0/OpenTPW.dll --game "/path/to/Sim Theme Park"
```

That is the whole setup. The libraries OpenTPW needs travel with the build.

The one thing you may need to install is a **Vulkan driver**. On Windows and macOS your graphics driver already provides it. On Linux, install your distribution's Vulkan loader (`libvulkan1`, `vulkan-loader` or `vulkan-icd-loader`, depending on the distribution) and Mesa or your GPU vendor's driver.

## Getting the game's files

**Copy the disc keeping its long file names.** This is the most common way to end up with a copy that cannot work. A CD carries two sets of file names, and the plain one is uppercase and cut short - `CHALLE~0.SAM` where the game asks for `Challenges.sam`. Mount the disc and copy from the mount, or use a tool that keeps the long (Joliet) names. OpenTPW notices a short-name copy and tells you.

**Telling OpenTPW where the game is.** Either put the build in the game's folder, next to where `TP.exe` was, or point at the folder:

```sh
OpenTPW --game "/path/to/Sim Theme Park"
OPENTPW_GAME_PATH="/path/to/Sim Theme Park" OpenTPW
```

Quote the path if it has spaces in it. If OpenTPW cannot find the game, it stops and lists every folder it looked in.

<details>
<summary>Exactly where it looks</summary>

A folder counts as the game when it contains a `data` folder with the game's `levels` inside it. Capitalisation never matters.

It tries, in order: `--game` on the command line, `OPENTPW_GAME_PATH`, the `GamePath` setting if you have changed it, the folder the build sits in, the working directory, and the default Windows install location. It does not search parent folders.

Saves go to `save/` beside the game's data, where the original keeps them, so the options and players saved by one are seen by the other.

</details>

## Controls

| Key | Does |
|-----|------|
| `Escape` | Game menu - or, in a park, first let go of what you are holding, or leave the ground view |
| `Ctrl`+`H` | Show or hide the help bar |
| `F2` | Hide the interface |
| `X` | Freeze the lobby camera |
| `[` `]` | Move between islands |
| `C` | In a park: walk on the ground and look around, and back again |
| `←` `→` | Turn the park camera |
| Mouse wheel | Zoom the park camera |

## Platforms

| Platform | Builds | Runs |
|----------|--------|------|
| Linux x64 | ✅ | ✅ Developed and tested here |
| Linux arm64 | ✅ | ❔ Never run |
| Windows x64, arm64 | ✅ | ❔ Not run recently |
| macOS Intel, Apple Silicon | ✅ | ❔ Never run |

Vulkan is the only graphics backend on every platform; macOS reaches it through MoltenVK, which ships with the build. Only Linux x64 is tested - bug reports from anything else are very welcome.

<details>
<summary>Linux on Wayland</summary>

The SDL that ships with OpenTPW has no Wayland backend, so a Wayland session runs it through XWayland. That works, but scales and handles input a little differently, and without XWayland installed it will not start. If your distribution's SDL2 is better, use it:

```sh
OPENTPW_SYSTEM_SDL=1 dotnet source/OpenTPW/bin/Debug/net10.0/OpenTPW.dll --game "/path/to/Sim Theme Park"
```

</details>

## Troubleshooting

**"Theme Park World was not found."** Point OpenTPW at the game with `--game <folder>` or `OPENTPW_GAME_PATH`, or put the build in the game's folder.

**"its names have been cut short."** The disc was copied without its long file names. Copy it again - see [Getting the game's files](#getting-the-games-files).

**It stops before a window appears, saying it could not load a native library.** This is almost always the Vulkan loader or the graphics driver. Install your distribution's Vulkan loader and your GPU's driver.

**On Wayland the window scales oddly, or SDL says "No available video device".** Install XWayland, or see [Linux on Wayland](#platforms).

**No sound.** Not fatal - OpenTPW warns and carries on.

## For developers

```sh
dotnet build source/OpenTPW.sln
dotnet test source/OpenTPW.sln
```

Most tests read the real game files and skip when the game is not found; set `OPENTPW_GAME_PATH` to run them all. `OPENTPW_DEBUG_CONSOLE=1` reads commands from standard input, for driving a run.

- [`docs/STATUS.md`](docs/STATUS.md) - what works and what does not.
- [`docs/QUEUE.md`](docs/QUEUE.md) - the work queue.
- [`docs/exe/`](docs/exe) - what the original executable does, decoded.
- [OpenTPW.FileFormats](https://github.com/OpenTPW/OpenTPW.FileFormats) - the game's file formats, documented.

<details>
<summary>File format support</summary>

❌ Not implemented &nbsp;&nbsp; ⚠️ Partially implemented &nbsp;&nbsp; ✅ Implemented

| Format | Status |
|--------|--------|
| Archives (.WAD, .SDT) | ✅ |
| Textures (.WCT) | ✅ |
| Settings (.SAM) | ✅ |
| Sounds (.SDT, .MP2) | ✅ |
| Strings (.BFMU, .BFST) | ⚠️ Read; four long strings are cut short |
| Fonts (.BF4) | ✅ |
| Lip sync (.LIP) | ✅ |
| Particles (.PLB, .ESP, .TPC) | ✅ |
| Machine and player saves (Config.tcf, gms.dat) | ✅ |
| Map data (.MAP) | ✅ |
| Texture tables (.TCT) | ✅ |
| Models (.MD2) | ⚠️ Meshes load; five of the animation channel kinds are decoded |
| Sound categories (cat_\*.map) | ⚠️ Decoded; a few fields are unnamed |
| Park signs (.SGN) | ⚠️ Read and drawn; a gradient fill is drawn flat |
| Park saves (.TPWS) | ⚠️ Read, only Lost Kingdom's shipped save tested; nothing writes one |
| Ride scripts (.RSE) | ⚠️ Every shipped script runs; 74 of 106 instructions built |
| Materials (.MTR) | ❌ |
| Video (.TQI) | ❌ |

</details>

## Contributing

Contributions are very welcome:

1. Fork the [original project](https://github.com/OpenTPW/OpenTPW).
2. Create a branch named `YourName/FeatureName`.
3. Open a pull request when you are ready.

## License

MIT - see [LICENSE.md](LICENSE.md).
