# Workflow

The workflow rules from `CLAUDE.md`, with the traps behind them. `CLAUDE.md` loads every session and this
file does not, so where the two differ `CLAUDE.md` wins.

## Branches

- See `CLAUDE.md`, Branches. Fast-forward with `git checkout main && git merge --ff-only <branch>`; this
  sandbox refuses `git branch -f main`.
- Name a branch for what it changes, not with a phrase.

## Pushing

- **Never push, and never open a pull request, without a fresh yes from Alexah.** Commit locally as work finishes. Say what is ready, which branches, and where it would go. Then wait.
- When the yes comes, it covers everything of Alexah's that is not yet up. Survey every local branch in **both** clones against origin with `git ls-remote`, not a remembered list, and do not narrow it to what you named. Two stay behind unless asked for by name: a branch carrying an open pull request, and `wf-review-*` scratch branches.
- Push only to `origin` (Alexah's fork), one explicit refspec per branch, no force, no pull request. Run it with `GIT_TERMINAL_PROMPT=0` and the askpass variables unset so a missing login fails fast, then read the tips back with `git ls-remote origin`.
- Before a push, every commit is built **and tested** alone in a throwaway worktree. CI is refused; this ritual is what replaces it.
- Stage files explicitly. Never `git add -A`. Check `git worktree list` and `git branch --list 'worktree-*'` for leftovers from review agents.

Four things about that gate, each of which has cost a session:

- **Put the worktree on real disk, not the session scratchpad.** The scratchpad is a small tmpfs; when it fills, commands exit 0 with truncated output, so the gate appears to pass while measuring nothing.
- **A green working-tree build is not evidence that a commit compiles.** `git mv` stages a rename using the file's *indexed* content, so a class rename can stay unstaged and the commit fails to build even though your tree is fine.
- **`git add -p` is unavailable here.** When one file carries changes belonging to two commits, re-cut the commits so their file sets are *disjoint*; do not try to split a file.
- **A test count in a commit message is a claim about that commit standing alone**, and a full-suite run cannot check it. Either gate the commit alone or do not quote a count.

## Sessions

- **One task per session.** Start Claude Code from the repo root. Read `CLAUDE.md` (automatic) and `docs/STATUS.md`, take the first unticked item in `docs/QUEUE.md` (unless Alexah names another), then read the one `docs/exe/` page for its area. Check the Ghidra headless server at the start of work (`CLAUDE.md` rule 7).
- Keep a multi-step task's checklist in the live plan and tick each step before moving on (rule 16).
- End the session when the task is committed, its `docs/QUEUE.md` item ticked and `docs/STATUS.md` updated. Do not carry the next task in the same context. When Alexah says a clear is coming, read `docs/QUEUE.md` and show the next five unticked items with what each asks (rule 17).
- Memory files hold rules and the live plan only, each under 300 lines, and nothing that is finished. Facts go in `docs/`. Corrections replace old text.

## Commits

- Small. One behaviour per commit.
- The message says **why**, and what **evidence** was gathered: the test names, the debug-console run, the addresses traced, the number predicted and the number seen.
- Executable facts go into `docs/exe/` in the same commit; file-format facts go into the FileFormats docs clone (a separate repo) in the same session. A fact only in a commit message or a memory file is a fact the next session will not find.

## Verifying

- Build with `--no-incremental`, then `dotnet test --no-build` with `OPENTPW_GAME_PATH` set; without it the game-data tests skip, so read the skip count, not just "Passed!". Take the counts fresh; never compare against a remembered number.
- Confirm in the running game, launched with `--game` or `OPENTPW_GAME_PATH`, with `OPENTPW_DEBUG_CONSOLE=1` and `SDL_AUDIODRIVER=dummy`. Predict the number before observing it.
- Put the bug back and re-run. If the suite stays green, the test is hollow: extract the decision into a pure function and pin that.
- Say what was **not** verified.
