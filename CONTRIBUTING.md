# Contributing to OpenTPW

Thank you for helping. OpenTPW recreates Theme Park World (1999) as closely as it can, on modern computers. This page
is for people; the rules every contributor works by, and what a coding agent reads, are in [`AGENTS.md`](AGENTS.md).
If you work with an agent, point it at that file (Claude Code reads it through `CLAUDE.md` by itself) and give it the
task yourself: an agent does what its person asked and does not pick work from the queue on its own. To learn how the
code is laid out, read [`ARCHITECTURE.md`](ARCHITECTURE.md).

## Do you need the game?

**Not for everything.** OpenTPW ships no game content, and nothing taken from the game's files (bytes, textures,
sounds, text) may ever be committed.

| | Needs your own copy of the game? |
|---|---|
| Documentation, the website text, queue notes | No |
| Tools and scripts (`tools/`), the Python harness libraries | No |
| Building, and running the tests | No. Without the game, the tests that need it skip: on 2026-10-28, 1,648 of the 4,285 ran and 2,637 skipped, with 0 failing. Read the skip count: a pass with skips is not a full pass. |
| Playing the game, and confirming that a change to what is drawn or how the game behaves is right | **Yes** |
| The harnesses that drive the running game or the original under Proton | **Yes**, and a Linux machine for the Proton ones |

Without the game you can still do real work and check it. With it, you can also see your change in the running game,
which is what finishes most code changes. [Getting the game's files](README.md#getting-the-games-files) says how to
copy your disc.

## Quick start

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0), `git`, and `python3` for the hooks
(Python 3.8 or newer). Fork [maexah/OpenTPW](https://github.com/maexah/OpenTPW) and clone your fork first.

**Linux or macOS**

```sh
cd OpenTPW
git config core.hooksPath tools/hooks          # once per clone: refuses a commit that stages the game's files
dotnet build source/OpenTPW.sln --no-incremental
dotnet test  source/OpenTPW.sln --no-build     # no game needed
```

With a game folder, name it on every run and test run, and every test runs:

```sh
export OPENTPW_GAME_PATH="/path/to/Sim Theme Park"
dotnet test source/OpenTPW.sln --no-build
dotnet source/OpenTPW/bin/Debug/net10.0/OpenTPW.dll --game "$OPENTPW_GAME_PATH"
```

On Linux the game also needs a Vulkan driver (`libvulkan1` and Mesa, or your GPU vendor's). With no display, or to try
a build on a machine with no graphics card: `Xvfb :99 &` then
`DISPLAY=:99 VK_DRIVER_FILES=/usr/share/vulkan/icd.d/lvp_icd.json SDL_AUDIODRIVER=dummy dotnet source/OpenTPW/bin/Debug/net10.0/OpenTPW.dll --game "<game folder>"`
(lavapipe is the software Vulkan driver, and draws slowly).

**Windows** (PowerShell)

```powershell
cd OpenTPW
git config core.hooksPath tools/hooks
dotnet build source\OpenTPW.sln --no-incremental
dotnet test  source\OpenTPW.sln --no-build
$env:OPENTPW_GAME_PATH = "C:\path\to\Sim Theme Park"
dotnet source\OpenTPW\bin\Debug\net10.0\OpenTPW.dll --game "$env:OPENTPW_GAME_PATH"
```

**What has been tried.** *The Linux steps are tested on the maintainer's Linux machine only* (copy the tracked files,
build, the tests without the game, a refused staged `.wad`, a launch under Xvfb and lavapipe that reached the lobby;
run on 2026-10-28 by `tools/harness/lib/quickstart_check.sh` from an empty home folder and a cleaned environment, no
container). *These launch steps are not tested on Windows or macOS* until someone runs them there; the build and the
tests are plain .NET and should work. If you try them, say how it went in an issue.

**Advanced (the maintainer's tooling, Linux only).** Ghidra on `testme.exe`, the Python harnesses in
[`tools/harness/`](tools/harness/README.md) that drive the game and read park files, and the original game under Proton
([`tools/play-the-original`](tools/play-the-original/README.md)). You do not need any of them to contribute. When a
task needs one, [`docs/TOOLING.md`](docs/TOOLING.md) lists each tool and the recipes found by trial.

## Choosing what to do

- **Lost Kingdom comes first.** Work on the jungle park (`data/levels/jungle`) is what is wanted now; the other three
  themes wait.
- **Start from a "good first" issue.** A few queue items will be made into issues with the label `good first issue`. Each
  names its Q-number, where to look, what is expected, and the tools it needs. They are small, and none needs Ghidra.
- **Or pick from the queue.** [`docs/QUEUE.md`](docs/QUEUE.md) is the maintainer's work list; take an item with no
  `Claimed:` line. Read [`docs/STATUS.md`](docs/STATUS.md) first for what works today. For anything big, or not in the
  queue, open an issue first so nobody does the same work twice.
- **Say you have taken it.** Comment on the issue; the item gets a `Claimed: @you, #123, <date>` line. A claim lapses
  after 14 quiet days. [`docs/WORKFLOW.md`](docs/WORKFLOW.md), "Claims", has the details.
- **Questions** are welcome as issues titled "Question: ...". Nobody minds a question before the work; a wrong
  guess costs more.

## How the work is done

The project's habits, in short. `AGENTS.md` has them all, numbered.

- **Don't guess, verify.** A fact about a file format is measured across every shipped file, not one. A fact about
  what the game does comes from the disassembly ([`docs/exe/`](docs/exe)). Where you could not check something,
  say so.
- **Confirm it, and write down how.** Predict what you will see, then run it and compare ("Showing how you checked",
  below). A change to what is drawn is confirmed with a screenshot of the running game.
- **Test the test.** Put your bug back and re-run: if the tests still pass, they do not test the fix.
- **Unbuilt paths are counted, not guessed.** Code the game reaches but we have not built calls
  `Unimplemented.Report( "NAME" )`.
- **A difference from the original is said where it is made**: in the code, and in your pull request.
- **Write facts down once.** File layouts go to [OpenTPW.FileFormats](https://github.com/maexah/OpenTPW.FileFormats);
  what the executable does goes to `docs/exe/`. Comments point at them.
- **Describe what the executable does; never paste decompiled code.** If you use Ghidra on `testme.exe`, the page
  you write says what a function does, its address, and what you measured, in your words. Decompiler output,
  disassembly listings and bytes from the game's files stay on your machine. `docs/exe/` is the shared record.
- **Style**: tabs, LF line endings, file-scoped namespaces, spaces inside parentheses (`Foo( x, y )`), braces on their
  own line. Match the file you are in. `.gitattributes` and `.editorconfig` keep line endings LF on every system.

### Showing how you checked

Put a short record in your pull request, or in a file you add under the item (the maintainer will move it to the
right page). It is portable: someone with the same data can repeat it.

1. **Prediction.** What you expect to see, as a number or a line, written before you run it.
2. **Command.** The exact command or clicks, and the game build (the first lines of `console.log` name it).
3. **Lines.** The census or log lines the run printed.
4. **Result.** Held, or missed with the cause. A miss is recorded, then predicted again.

A screenshot is welcome where it really helps (anything drawn). Attach it to the pull request or issue rather than
committing it: a committed picture stays in the history after it is removed. Pictures of the game's own art, raw
textures and models are not committed at all, and `content/ReferenceScreenshots/` is ignored on purpose.

### Before you open a pull request

No CI tests the code, on purpose. The one workflow, [`weekly.yml`](.github/workflows/weekly.yml), only packages a
weekly build from a tag the maintainer pushes and runs the content guard that keeps the game's files out of the zip; it
runs no test and decides nothing about your change. So the check is yours. Build and test each commit in a clean
worktree, and run the content guard on what it changes. One command does all three and prints a summary (the build's
errors and warnings, the tests passed, failed and skipped, whether the game was given, the guard's answer):

```sh
tools/check_commit.sh                      # HEAD; add a commit, and your game folder for a full pass
tools/check_commit.sh HEAD "<game folder>"
```

## Pull requests

1. One task per branch, off `main`, named `<you>/<what-it-changes>`.
2. Commit as you go. Say in the commit message what changed and how you checked it.
3. Open a pull request against `maexah/OpenTPW` `main` and fill in the template: the test counts, a screenshot if
   anything drawn changed, and anything you could not check. If it finishes a queue item, name the Q-number; the
   maintainer ticks the item and updates `docs/STATUS.md` when it is merged, so you leave both alone.
4. The maintainer reviews it, tries it in the game, and fast-forwards it into `main`. Pull requests are not
   squashed, so keep the history readable.
5. **A format finding** goes to [OpenTPW.FileFormats](https://github.com/maexah/OpenTPW.FileFormats). If you would
   rather not write it up there, put the finding in your pull request here (the layout, what you measured, over which
   files) and the maintainer writes it into that repository. If you do open a pull request there, link the two.

## Reporting a bug

[Open an issue](https://github.com/maexah/OpenTPW/issues/new/choose). Attach `console.log` from the game's folder,
beside `save`: it names the build, your system and your graphics card. Your home folder is written as `~` in it, but read it before you attach it publicly. If the game's
folder cannot be written, or OpenTPW found no game to start, the file is in your own data folder instead
(`%AppData%\OpenTPW` on Windows, `~/.local/share/OpenTPW` on Linux, `~/Library/Application Support/OpenTPW` on macOS).

## License

By contributing you agree that your work is released under the [MIT license](LICENSE.md). Anything you add that is
not your own work (a library, a font, an image) gets a row in [`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md) in the
same commit: a test fails when a package or a file under `content/` has none.
