# Contributing to OpenTPW

Thank you for helping. OpenTPW recreates Theme Park World (1999) as closely as it can, on modern computers. This page
is for people; the rules every contributor works by, and what a coding agent reads, are in [`AGENTS.md`](AGENTS.md).
If you work with an agent, point it at that file (Claude Code reads it through `CLAUDE.md` by itself).

## Before you start

- **You need your own copy of the game.** OpenTPW ships no game content, and nothing taken from the game's files
  (bytes, textures, sounds, text) may ever be committed. [Getting the game's files](README.md#getting-the-games-files)
  says how to copy your disc.
- **Lost Kingdom comes first.** Work on the jungle park (`data/levels/jungle`) is what is wanted now; the other three
  themes wait.
- **Pick something from the queue.** [`docs/QUEUE.md`](docs/QUEUE.md) is the work queue, taken from the top. Read
  [`docs/STATUS.md`](docs/STATUS.md) first for what works today. For anything big, or not in the queue, open an issue
  first so nobody does the same work twice.

## Set up

1. Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) and a Vulkan driver (see the
   README's [Build it yourself](README.md#build-it-yourself)).
2. Fork [maexah/OpenTPW](https://github.com/maexah/OpenTPW) and clone your fork.
3. Turn on the repo's git hooks, once per clone:

   ```sh
   git config core.hooksPath tools/hooks
   ```

4. Build and test, naming your game folder:

   ```sh
   export OPENTPW_GAME_PATH="/path/to/Sim Theme Park"
   dotnet build source/OpenTPW.sln --no-incremental
   dotnet test  source/OpenTPW.sln --no-build
   ```

   Without `OPENTPW_GAME_PATH` most tests skip. A pass with skips is not a full pass: read the skip count.

The test harnesses (Python scripts that drive the running game, read park files and put bugs back on purpose) are in
[`tools/harness/`](tools/harness/README.md). [`docs/TOOLING.md`](docs/TOOLING.md) lists each one and the recipes
found by trial.

## How the work is done

The project's habits, in short. `AGENTS.md` has them all, numbered.

- **Don't guess, verify.** A fact about a file format is measured across every shipped file, not one. A fact about
  what the game does comes from the disassembly ([`docs/exe/`](docs/exe)). Where you could not check something,
  say so.
- **Confirm in the running game.** A change to what is drawn is confirmed with a screenshot of the running game.
  Predict what you will see before you look.
- **Test the test.** Put your bug back and re-run: if the tests still pass, they do not test the fix.
- **Unbuilt paths are counted, not guessed.** Code the game reaches but we have not built calls
  `Unimplemented.Report( "NAME" )`.
- **A difference from the original is said where it is made**: in the code, and in your pull request.
- **Write facts down once.** File layouts go to [OpenTPW.FileFormats](https://github.com/maexah/OpenTPW.FileFormats);
  what the executable does goes to `docs/exe/`. Comments point at them.
- **Style**: tabs, LF line endings, file-scoped namespaces, spaces inside parentheses (`Foo( x, y )`), braces on their
  own line. Match the file you are in.

There is no CI, on purpose. Before you open a pull request, build and test your branch in a clean worktree:

```sh
git worktree add ../opentpw-check HEAD
cd ../opentpw-check
dotnet build source/OpenTPW.sln --no-incremental && dotnet test source/OpenTPW.sln --no-build
cd - && git worktree remove ../opentpw-check
```

## Pull requests

1. One task per branch, off `main`, named `<you>/<what-it-changes>`.
2. Commit as you go. Say in the commit message what changed and how you checked it.
3. If your work finishes a queue item, tick it in `docs/QUEUE.md` and update `docs/STATUS.md` in the same commit.
4. Open a pull request against `maexah/OpenTPW` `main` and fill in the template: the test counts, a screenshot if
   anything drawn changed, and anything you could not check.
5. The maintainer reviews it, tries it in the game, and fast-forwards it into `main`. Pull requests are not
   squashed, so keep the history readable.

## Reporting a bug

[Open an issue](https://github.com/maexah/OpenTPW/issues/new/choose). Attach `console.log` from the game's folder:
it names the build, your system and your graphics card, and holds no personal details.

## License

By contributing you agree that your work is released under the [MIT license](LICENSE.md).
