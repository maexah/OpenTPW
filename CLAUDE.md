# OpenTPW

A re-implementation of Theme Park World (1999) in C# / .NET 10 on NeoVeldrid (Vulkan).
It reads the original game's data files. It ships no game content.

This file is loaded on every session, in full. It holds the rules and the map.
Machine-specific paths live in `CLAUDE.local.md` (not committed). Long-form knowledge lives in `docs/`.

## The goal, in Alexah's words (2026-09-16)

> Recreate the original as closely as possible unless there is a real, genuine burden. Get the original
> running on modern computers, add some fixes that are nice, and eventually a mod API once the game is
> 100% complete. The mod API is not the focus.

- **Scope: 100% Lost Kingdom first.** That is `data/levels/jungle` and its `Easymode.TPWI`. Do not spend effort on the other three themes yet.
- **Don't guess, verify.** A claim about a binary layout is measured across all the game's data (all 2,129 `.md2`, all 308 `.RSE`, all 46 sprite banks), never inferred from one file.
- **A deviation from the original is said at the site**, in the code, and in the report.

## Standing rules (each was set by Alexah; the date is when)

1. **Never push, and never open a pull request, without a fresh yes** (2026-09-10). Commit locally as work finishes. Say what is ready and where it would go, then wait. When the yes comes, it covers everything of Alexah's that is not yet up (2026-09-18): survey every local branch in both clones against origin with `git ls-remote`, not a remembered list. Two stay behind unless asked for by name — a branch carrying an open pull request, and `wf-review-*` scratch branches.
2. **CI is refused** (2026-09-12). Do not propose it. The local ritual replaces it: build **and test** every commit alone in a throwaway worktree before it is pushed.
3. **Dead code is labelled, not deleted** (2026-09-12). Classify before calling anything a defect: dead by **CODE** (nothing calls it: a defect), by **CONTENT** (built, but nothing placed reaches it: not a defect), by **GAP** (placed content reaches it and it is unbuilt: the real queue). `VM/` stays as it is; the question is closed.
4. **Every unbuilt path the program reaches calls `Unimplemented.Report( "NAME" )`.** Never guess an opcode or a field. A counted no-op beats a wrong implementation.
5. **Debug logging is expected wherever it helps** (2026-09-18). It will be stripped later by a debug/production split. Performance tooling still goes behind one off-by-default switch with one call site. Throwaway scripts stay in the scratchpad.
6. **Confirm in the running game, never from code alone** (said three times). A change to what is drawn is confirmed by looking at a screenshot of the running game (2026-09-18). Predict the number before observing it. Put the bug back and re-run: if the suite stays green, the test is hollow.
7. **Ghidra is mission critical.** Check the headless server at the start of work. If a tool Alexah provided is broken, say so and walk them through fixing it. Do not work around it.
8. **Think it through step by step before acting**, on every request. Do not make replies longer because of it.
9. **New systems are modular and data-driven**: engine and content apart, legacy formats read at the boundary, no layer built for a consumer that does not exist, no unrelated rewrites.
10. **Movement, rotation and easing use `Time.Delta`**, never per-frame constants. Easing uses `Time.SmoothingFactor`.
11. **Where the original is blank or broken at a risky point** (an empty confirm box), fill the gap in the game's own style and say so.
12. **Test runs are visible and audible on Alexah's desktop.** Run verification with `SDL_AUDIODRIVER=dummy`, use the game's real paths, drive only what the real game would do (`greet`, not an arbitrary speech sample), and never wipe `save/`. Delete only what your run created.
13. **Review agents share the working tree.** Pass `isolation: 'worktree'` or say read-only. Never build or test while a workflow is live. Clean up `.claude/worktrees/` and `worktree-*` branches afterwards. Stage files explicitly; never `git add -A`.
14. **File-format facts go to the FileFormats docs clone in the same session** (bytes in a shipped file). **Executable facts go to `docs/exe/`** (what the game does with them). See `docs/README.md`.
15. **Do not mention the Gmail, Calendar or Drive connectors.**
16. **Write the plan down as you go** (2026-09-11). Put a multi-step task's checklist in the live plan and tick it before moving on, not at the end, so an auto-compaction loses nothing. Hand broad read-only sweeps to subagents that report a summary; keep edits, builds, commits and game launches in the main session.
17. **Show the next five queue items before every clear** (2026-09-23). When Alexah says they are about to clear the session, show the next five unticked items in `docs/QUEUE.md`, in queue order, each with what it asks for. **Read the file and show what it says**, rather than reciting it from memory; a half-remembered list handed over at a clear is the one place a stale claim cannot be caught later.
18. **Spend subagent tokens where they buy accuracy** (2026-09-26). Pick each agent's model by its stage, fan out only after a script has done what a script can, audit incrementally, and check `/usage` before a large run. The table and the rules are `docs/WORKFLOW.md`, "Subagents".

## Build, test, run

```sh
dotnet build source/OpenTPW.sln --no-incremental    # 0 errors expected; the known warnings are left alone
dotnet test  source/OpenTPW.sln --no-build          # ~1 s. Without the game, every test that needs it skips
OPENTPW_GAME_PATH="<game folder>" dotnet test source/OpenTPW.sln --no-build   # every test runs, none skip
dotnet source/OpenTPW/bin/Debug/net10.0/OpenTPW.dll --game "<game folder>"
OPENTPW_DEBUG_CONSOLE=1 ...                          # commands on stdin; `unimplemented` prints the gap census
```

- `dotnet test` does not accept `--no-incremental`; build first, then test with `--no-build`.
- Tests that need the game call `GameData.Required()` and skip when it is absent. A green run with skips is normal without the game; compare the counts with the dated ones in `docs/STATUS.md`, "Numbers", never with a figure written here.
- Take every count fresh (tests, warnings, opcodes). Never quote one from memory; the numbers in prose go stale within a day.

## Where things are

| Project | What it is |
|---|---|
| `OpenTPW.Common` | Window, display, virtual file system (`BaseFileSystem`; the `Zio` package it references is used by nothing), vector types, `Logger`, `Unimplemented`. `GlobalNamespace.cs` holds the static `Log`, `FileSystem`, `SaveFileSystem`, `CacheFileSystem`. |
| `OpenTPW.Files` | Binary format readers. `Formats/<kind>/`. `Public/` is the old upstream API: its `SettingsFile`, `StringFile` and `BaseFormat` are still the live entry points; `MapFile` and `Public/ModelFile.cs` are used by nothing, and `SoundFile` is live only through `SdtArchive`, which reads every `.sdt` entry with it. |
| `OpenTPW` | The game. `Program.cs` → `Client/Game.cs` (boot order lives here) → `World/Level.cs` (builds a scene). |
| `OpenTPW.Tests` | MSTest, flat folder, named by area. `GameData.cs` is the shared fixture. |
| `OpenTPW.ModKit` | Upstream ImGui file browser. Referenced by no project. Alexah's call; leave it. |

Inside `source/OpenTPW/`: `Client/` (startup, `GameDir`, options, renderer, `Diagnostics/DebugConsole.cs`), `World/Park/` (the running park: start with `ParkState`, `ParkPeople`, `PeepBehaviour`, `ParkRides`), `VM/RideScript.cs` (the live VM), `UI/` (the game's own UI: `UiControl`, `WindowStack`; `UI/FrontEnd/` lobby, `UI/Park/` gadget), `Audio/`.

## Words that mean one thing

- **Peep** = any person in the park, guest or staff. Prefer it over Guest / Visitor / Person in new code.
- **Thing** = anything with a `ThingId` in the save. **Object** = a placed shop, ride or scenery (`CatalogueObject`). **Item** = the catalogue definition (`ItemDescriptionFile`).
- **ParkWorld** = the save FILE, immutable. **ParkState** = the running numbers and cells. Never write to `ParkWorld`.
- **Time** = frame clock; no menu pauses it (only the debug console's `pause`/`step` hold it). **GameClock** = the 31 ms game tick; a park's menu pauses it, nothing in the lobby does. **GameCalendar** = the in-game date.
- **RideScript** is the VM. `RideVM` (with `Instruction`, `Operand`, `Branch`, `OpcodeHandlerAttribute`), `VM/Handlers/`, `VM/Includes/ScriptDefs.cs` and `World/Ride.cs` are upstream leftovers reached only through `World/Ride.cs`, which nothing constructs, and `VM/Includes/Events.cs` is referenced by nothing: do not extend. `VM/Includes/ParLib.cs` is live and is a particle table, not VM code.
- Reach script variables **by name**, never by index. Companion scripts declare none of the common twelve.
- Themes: `jungle`, `fantasy`, `hallow`, `space`, lower-case once inside `Level`. The map is 128 × 128 cells; positions in the save are fixed-point (`FixedVector`); a packed cell id is `y*128 + x + 1`.

## Conventions

- Tabs, LF, file-scoped namespaces. Spaces inside parentheses: `Foo( x, y )`. Braces on their own line. Match the file you are in.
- A comment says what the code does **now**. It may cite one address and one `docs/exe/` row. History, evidence and corrections go in the commit message. When a belief changes, replace the comment; never write "this used to say".
- Reverse-engineered facts are written **once**: file layouts in the FileFormats docs, executable behaviour in `docs/exe/`. Code comments and commit messages point at them.

## Branches
- main has everything and is the tip. A fresh session starts from main.
- One short branch per task, alexah/N-<what-it-changes>, based on main.
  When the task is confirmed in the game, fast-forward merge it into main.
- upstream/main is the mirror of the upstream project. Merge it into main
  when it moves. Never rebase or force-push main.

## How a session runs

1. **Start from the repo root**, always. Claude Code keys its memory to the working directory; a session started in `source/` has none.
2. Read `docs/STATUS.md`, then the first unticked item in `docs/QUEUE.md`, then the one `docs/exe/` page for the area you are about to touch. Do not read `docs/history/` (the former memory archives) whole; grep it, and never quote it as current.
3. Before adding a helper, grep for it. `PeepWalk`, `CellRoute`, `CellSearch`, `MapStep`, `FixedVector`, `ParkState.CellAt` already exist.
4. One task per session. When the task is done: build and test alone in a worktree, commit, tick the item in `docs/QUEUE.md` and update `docs/STATUS.md` in the same commit, say what is ready, and stop. Do not start the next task in the same session.
5. Memory files (`~/.claude/projects/.../memory/`) hold rules and the live plan only, each under 300 lines. Facts go in `docs/`. A correction replaces the old text; no `>>>` markers, no "this line said".
