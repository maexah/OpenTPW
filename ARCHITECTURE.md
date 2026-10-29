# Architecture

Where things are. `AGENTS.md` holds the rules; [`CONTRIBUTING.md`](CONTRIBUTING.md) is the guide for people;
`docs/STATUS.md` says what works. A path or type named on this page is checked against the tree by
`tools/check_docs_paths.py`, which the maintainer's Saturday audit runs.

| Project | What it is |
|---|---|
| `OpenTPW.Common` | Window, display, virtual file system (`BaseFileSystem`), vector types, `Logger`, `Unimplemented`. `GlobalNamespace.cs` holds the static `Log`, `FileSystem`, `SaveFileSystem`, `CacheFileSystem`. |
| `OpenTPW.Files` | Binary format readers. `Formats/<kind>/`. `Public/` is the old upstream API: its `SettingsFile`, `StringFile` and `BaseFormat` are still the live entry points; `MapFile` and `Public/ModelFile.cs` are used by nothing, and `SoundFile` is live only through `SdtArchive`, which reads every `.sdt` entry with it. |
| `OpenTPW` | The game. `Program.cs` → `Client/Game.cs` (boot order lives here) → `World/Level.cs` (builds a scene). |
| `OpenTPW.Tests` | MSTest, flat folder, named by area. `GameData.cs` is the shared fixture. |

Inside `source/OpenTPW/`: `Client/` (startup, `GameDir`, options, renderer, `Diagnostics/DebugConsole.cs`), `World/Park/` (the running park: start with `ParkState`, `ParkPeople`, `PeepBehaviour`, `ParkRides`), `VM/RideScript.cs` (the live VM), `UI/` (the game's own UI: `UiControl`, `WindowStack`; `UI/FrontEnd/` lobby, `UI/Park/` gadget), `Audio/`.

`tools/` holds the git hooks (`tools/hooks/`) and the content guard they run (`tools/content_guard.py`), the shared test
harnesses (`tools/harness/`), the weekly build's packaging (`tools/weekly/`), the player's guide to running the
original (`tools/play-the-original/`) and the maintainer's own hooks and sweeps (`tools/maintainer/`).

## Reading on

- What the executable does, system by system: `docs/exe/` (list a page's headings with `grep -n '^#'` before reading it).
- File layouts: the [FileFormats docs](https://github.com/maexah/OpenTPW.FileFormats).
- How a change is proven, and the stored tools: `docs/WORKFLOW.md`, "Verifying", and `docs/TOOLING.md`.
- The words this project uses for one thing each (peep, thing, object, item, `ParkWorld` against `ParkState`):
  `AGENTS.md`, "Words that mean one thing".
