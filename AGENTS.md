# OpenTPW

A re-implementation of Theme Park World (1999) in C# / .NET 10 on NeoVeldrid (Vulkan).
It reads the original game's data files. It ships no game content.

This file holds the rules every contributor works by, human or agent, and where things are (`ARCHITECTURE.md` is the map). Any coding agent reads it
(Claude Code reads it through `CLAUDE.md`). The guide for people is `CONTRIBUTING.md`. Paths on your own machine go in
`CLAUDE.local.md` or your agent's equivalent, never committed. Long-form knowledge lives in `docs/`.

## The goal, in the maintainer's words (2026-09-16)

> Recreate the original as closely as possible unless there is a real, genuine burden. Get the original
> running on modern computers, add some fixes that are nice, and eventually a mod API once the game is
> 100% complete. The mod API is not the focus.

- **Scope: 100% Lost Kingdom first.** That is `data/levels/jungle` and its `Easymode.TPWI`. Do not spend effort on the other three themes yet.
- **Don't guess, verify.** A claim about a binary layout is measured across all the game's data (all 2,129 `.md2`, all 308 `.RSE`, all 46 sprite banks), never inferred from one file.
- **A deviation from the original is said at the site**, in the code, and in the report.

## How a task runs

1. **Start from the repo root**, always. Agents key their memory to the working directory.
2. **Do the task your person gave you, and only that.** An agent never takes a queue item unasked: `docs/QUEUE.md` is
   the maintainer's work list, not yours to start from. Read `docs/STATUS.md` (what works today), then, if your person
   named a queue item, that item (read the file's first hundred lines or so with a line limit, never the whole queue).
   Then the one `docs/exe/` page for the area you are about to touch: those run to 550 KB, so list its headings (`grep -n '^#'`)
   and read the sections the item needs, never the page whole. Do not read `docs/history/` whole; grep it, and never quote it as current.
3. Before adding a helper, grep for it. `PeepWalk`, `CellRoute`, `CellSearch`, `MapStep`, `FixedVector`, `ParkState.CellAt` already exist. A harness the same: `docs/TOOLING.md` lists every stored tool and the recipes found by trial; `docs/WORKFLOW.md`, "Verifying", says how a change is proven. Read both before writing a harness script, and add to them what you build (rule 21).
4. One task at a time. When it is done: commit, then build and test that commit alone in a worktree
   (`tools/check_commit.sh` checks a commit, never uncommitted changes); fix what fails, commit again and re-check
   until it passes; say what is ready, and stop. A
   contributor leaves `docs/QUEUE.md` and `docs/STATUS.md` alone: the maintainer ticks and updates them when the work
   is merged (`docs/WORKFLOW.md`, "Claims"). The maintainer's own sessions follow `docs/WORKFLOW.md`, "Sessions" and
   "Unattended runs", which tick the item and update STATUS in the same commit. If the item changes something `README.md`
   claims (what works, what is saved, where the log goes), change `README.md` in the same commit.
5. Agent memory holds rules and the live plan only. Facts go in `docs/`.
6. **Say what you checked and what you did not.** Name the claims you established yourself (the source, a test, the
   running game, Ghidra), the ones you took from a doc, and the ones you could not check and why. Give a test run's
   command and its passed, failed and skipped counts: a pass with skips is not a full pass.

## Other agents and environments

The rules bind every agent; the means are your tool's own. No agent has to imitate Claude Code to contribute.

- **Claude Code's parts** are `CLAUDE.md`, `CLAUDE.local.md` and `.claude/` (its subagent types and hooks). Another
  agent uses its own equivalents: machine paths in your tool's uncommitted local file, never in the repo.
- **The hooks are git's**, so they run for any agent once `core.hooksPath` is set. Where they do not run, run
  `python3 tools/content_guard.py staged` before every commit.
- **Subagents**: the gather, sweep and verify stages are `docs/WORKFLOW.md`, "Subagents"; which model takes which kind
  of work is "Tiers" there. With no subagents, do the stages in order in one session: the verify still challenges
  every finding and hunts what the sweep missed.
- **Without git** (a copied snapshot): read, review, build and test, but you cannot branch or commit. Hand back a patch
  or a written report and say it was made without the history.
- **Without the game, Ghidra or Proton**: `CONTRIBUTING.md`, "Do you need the game?", says what you can still do, and
  rule 7 what to say about what you could not check.

## Rules

The maintainer set each of these; the date is when. Other pages cite them by number ("`AGENTS.md` rule 4"), so a
number never changes meaning. The numbers missing here (15, 17, 19, 20) are the maintainer's own working rules,
kept in their local file; they bind nobody else.

1. **Pushing and pull requests are your human's call** (2026-09-10). An agent commits locally as work finishes, says
   what is ready and where it would go, and waits for a yes. Outside work reaches `main` by a pull request to
   `maexah/OpenTPW` (`CONTRIBUTING.md`); the maintainer fast-forwards it in. Never force-push a shared branch, and
   never push onto a branch that carries someone else's open pull request.
2. **There is no CI, by choice** (2026-09-12). Do not propose it. The local ritual replaces it: build **and test**
   every commit alone in a throwaway worktree before it goes up. **One workflow is allowed** (2026-10-27): `weekly.yml`
   packages and publishes the weekly build from a tag the maintainer pushes. It runs no test and decides nothing
   about the code; its one refusal is the content guard (`docs/WORKFLOW.md`, "Weekly builds").
3. **Dead code is labelled, not deleted** (2026-09-12). Classify before calling anything a defect: dead by **CODE** (nothing calls it: a defect), by **CONTENT** (built, but nothing placed reaches it: not a defect), by **GAP** (placed content reaches it and it is unbuilt: the real queue). `VM/` stays as it is; the question is closed.
4. **Every unbuilt path the program reaches calls `Unimplemented.Report( "NAME" )`.** Never guess an opcode or a field. A counted no-op beats a wrong implementation.
5. **Debug logging is expected wherever it helps** (2026-09-18). It will be stripped later by a debug/production split. Performance tooling still goes behind one off-by-default switch with one call site. Throwaway scripts stay out of the repo.
6. **Confirm in the running game, never from code alone.** A change to what is drawn is confirmed by looking at a
   screenshot of the running game (2026-09-18). Predict the number before observing it. Put the bug back and re-run:
   if the suite stays green, the test is hollow.
7. **Executable facts come from the disassembly; Ghidra is mission critical.** The project decodes `testme.exe` in
   Ghidra (`docs/exe/`). If you have Ghidra, check it works before you start, and use it. If you do not, cite the
   `docs/exe/` row you relied on and say plainly what you could not check. A broken tool is reported and fixed, not
   worked around.
8. **Think it through step by step before acting**, on every request. Do not make replies longer because of it.
9. **New systems are modular and data-driven**: engine and content apart, legacy formats read at the boundary, no layer built for a consumer that does not exist, no unrelated rewrites.
10. **Movement, rotation and easing use `Time.Delta`**, never per-frame constants. Easing uses `Time.SmoothingFactor`.
11. **Where the original is blank or broken at a risky point** (an empty confirm box), fill the gap in the game's own style and say so.
12. **Test runs touch nothing of the player's** (2026-09-18). Run verification with `SDL_AUDIODRIVER=dummy` unless a
    person is listening, use the game's real paths, drive only what the real game would do (`greet`, not an
    arbitrary speech sample), and never wipe `save/`. Delete only what your run created.
13. **Agents that share a working tree do not build in it at the same time.** An agent working alone keeps only the
    last sentence. Give a review agent its own worktree or make it read-only. Never build or test while another agent
    is editing. Remove the worktrees and branches your agents made when done (Claude Code's are `.claude/worktrees/`
    and `worktree-*`). Stage files explicitly; never `git add -A`.
14. **File-format facts go to the FileFormats docs** ([maexah/OpenTPW.FileFormats](https://github.com/maexah/OpenTPW.FileFormats))
    in the same piece of work (bytes in a shipped file). **Executable facts go to `docs/exe/`** (what the game does
    with them). See `docs/README.md`.
16. **Write the plan down as you go** (2026-09-11). A multi-step task's checklist lives somewhere that survives a
    lost context (an agent's memory file, a scratch note), ticked before moving on, not at the end. Hand broad
    read-only sweeps to subagents that report a summary; keep edits, builds, commits and game launches in the main session.
18. **Spend subagent tokens where they buy accuracy** (2026-09-26). Pick each agent's model by its stage, fan out
    only after a script has done what a script can, and audit incrementally. The table and the rules are
    `docs/WORKFLOW.md`, "Subagents".
21. **A testing tool that will be wanted again is stored and listed, never written twice** (2026-10-10). Before
    writing a harness script, read `docs/TOOLING.md`, "The tools every item reuses", and use what is in
    `tools/harness/`. A reader, a poller or a driver a later item could use goes into `tools/harness/lib/`, not into
    your scratch folder, and gets its row in that list in the same commit. A recipe found by trial (the clicks that
    open a screen, the commands that fill a ride) goes in `docs/TOOLING.md`, "Recipes", not in a plan that will be cut.

## Build, test, run

```sh
dotnet build source/OpenTPW.sln --no-incremental    # 0 errors, 0 warnings; a new warning is fixed, never silenced
dotnet test  source/OpenTPW.sln --no-build          # ~1 s. Without the game, every test that needs it skips
OPENTPW_GAME_PATH="<game folder>" dotnet test source/OpenTPW.sln --no-build   # every test runs, none skip
dotnet source/OpenTPW/bin/Debug/net10.0/OpenTPW.dll --game "<game folder>"
OPENTPW_DEBUG_CONSOLE=1 ...                          # commands on stdin; `unimplemented` prints the gap census
OPENTPW_LOG_DIR=<folder> ...                         # where console.log / console-prev.log go; every harness launch of the game sets it
```

- `dotnet test` does not accept `--no-incremental`; build first, then test with `--no-build`.
- **Name the game folder on every launch and test run** (`--game` or `OPENTPW_GAME_PATH`). `GameDir` looks only in
  the build's folder and the working directory; a run with neither exits 1 after listing the folders it tried, and
  that is correct. Never commit a path into `source/OpenTPW/App.config`.
- Tests that need the game call `GameData.Required()` and skip when it is absent. A green run with skips is normal without the game; compare the counts with the dated ones in `docs/STATUS.md`, "Numbers", never with a figure written here.
- Take every count fresh (tests, warnings, opcodes). Never quote one from memory; the numbers in prose go stale within a day.
- The **original game** can run under Proton as a reference instrument (its frames, its live memory at Ghidra
  addresses, its timings): `docs/TOOLING.md`, "The original under Proton". Read its warnings first.

## Where things are

The map of the projects, the folders inside the game and `tools/` is [`ARCHITECTURE.md`](ARCHITECTURE.md).

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
- **No game content is ever committed**: no bytes from the game's files, no extracted textures, sounds or text. The
  weekly build's content guard refuses a package that holds any.

## Branches

- `main` has everything and is the tip. Start every task from it.
- One short branch per task, based on `main`. The maintainer's are `alexah/N-<what-it-changes>`; name yours
  `<you>/<what-it-changes>`. When the task is confirmed in the game it is fast-forward merged into `main`.
- `gh-pages` on origin is the fan website's finished files, not a branch of ours: leave it alone.
- `upstream/main` is the mirror of the upstream project. The maintainer merges it into `main` when it moves. Never
  rebase or force-push `main`.
- The FileFormats repo follows the same rules: its `master` is the tip, one short branch per task off it.
- Once per clone, enable the hooks: `git config core.hooksPath tools/hooks`. The pre-commit hook refuses a commit that
  stages a file of the game's (`tools/content_guard.py`). The queue and status sweeps and the website push are the
  maintainer's: they run only in a clone with `git config opentpw.maintainer true` (`docs/WORKFLOW.md`, "Hooks").
