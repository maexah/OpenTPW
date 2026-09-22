# Workflow

These are the rules already in force, written down where every session sees them.

## Branches

- `main` has everything and is the tip. A fresh session starts from `main`.
- One short branch per task, `alexah/N-<what-it-changes>`, based on `main`. When the task is confirmed in the game, fast-forward merge it into `main`.
- `upstream/main` is the mirror of the upstream project. Merge it into `main` when it moves. Never rebase or force-push `main`.
- Name a branch for what it changes, not with a phrase. One branch per task.

## Pushing

- **Never push, and never open a pull request, without a fresh yes from Alexah.** Commit locally as work finishes. Say what is ready, which branches, and where it would go. Then wait.
- When the yes comes, it covers everything of Alexah's that is not yet up. Do not narrow it to what you named.
- Before a push, every commit is built **and tested** alone in a throwaway worktree. CI is refused; this ritual is what replaces it.

Four things about that gate, each of which has cost a session:

- **Put the worktree on real disk, not the session scratchpad.** The scratchpad is a small tmpfs; when it fills, commands exit 0 with truncated output, so the gate appears to pass while measuring nothing.
- **A green working-tree build is not evidence that a commit compiles.** `git mv` stages a rename using the file's *indexed* content, so a class rename can stay unstaged and the commit fails to build even though your tree is fine.
- **`git add -p` is unavailable here.** When one file carries changes belonging to two commits, re-cut the commits so their file sets are *disjoint*; do not try to split a file.
- **A test count in a commit message is a claim about that commit standing alone**, and a full-suite run cannot check it. Either gate the commit alone or do not quote a count.
- Stage files explicitly. Never `git add -A`. Check `git worktree list` and `git branch --list 'worktree-*'` for leftovers from review agents.

## Sessions

- **One task per session.** Start Claude Code from the repo root. Read `CLAUDE.md` (automatic), `docs/STATUS.md`, then the one `docs/exe/` page for the area.
- End the session when the task is committed and `docs/STATUS.md` is updated. Do not carry the next task in the same context.
- Memory files hold the live plan and nothing that is finished. Facts go in `docs/`. Corrections replace old text.

## Commits

- Small. One behaviour per commit.
- The message says **why**, and what **evidence** was gathered: the test names, the debug-console run, the addresses traced, the number predicted and the number seen.
- The **facts** go into `docs/exe/` or the FileFormats docs in the same commit. A fact only in a commit message or a memory file is a fact the next session will not find.

## Verifying

- Build with `--no-incremental`, then `dotnet test --no-build`. Take the counts fresh; never compare against a remembered number.
- Confirm in the running game, with `OPENTPW_DEBUG_CONSOLE=1` and `SDL_AUDIODRIVER=dummy`. Predict the number before observing it.
- Put the bug back and re-run. If the suite stays green, the test is hollow: extract the decision into a pure function and pin that.
- Say what was **not** verified.
