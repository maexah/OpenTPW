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

## Subagents

Set by Alexah 2026-09-26, after one staleness audit spent about 40M subagent tokens and hit the weekly limit. None of
this removes a check: the adversarial verify and the review of applied edits stay.

**Model by stage.** Pass `model` per agent; omitting it runs Opus.

| Stage | Model | Why |
|---|---|---|
| Greps, listing files, headings or `case` labels, gathering evidence for a named question, formatting JSON or text | Haiku | A script checks the result |
| First-pass audit of a file, triaging notes, merging edits that agree, drafting queue items from verified findings, read-only sweeps | Sonnet | An Opus verifier checks every finding |
| Adversarial verify, reviewing applied edits, anything in Ghidra, decoding, merges that disagree on a fact, code changes | Opus | A wrong answer here costs a session |

A cheaper finder is safe only under an Opus verifier that also hunts misses; never Sonnet verifying Sonnet. Fable is
billed as its own weekly bucket: trial it against Opus on one slice before using it for a stage.

**Before fanning out.**
- Script what a script can first: heading citations, Q-number status, paths, `addresses.md`'s generator, test and
  member names cited in docs, `Unimplemented.Report` keys. A check build with the documentation file on reports every
  unresolved `<see cref>`. Agents then judge meaning only.
- Check `/usage`, estimate the run (an agent that reads whole files costs about 200-400k tokens), and ask Alexah before
  one that will not fit, or batch it so it does.
- Write a workflow's arguments to a file from the script that computed them and pass them unedited; never retype a
  file list.

**Shape.**
- Every file has exactly one auditor. Cross-cutting slices (a commit's drift, a names census) report leads to the
  owner, not edits, so no two agents rewrite one line.
- Audit incrementally: the files changed since the last audit (`git diff --name-only <last audit>..main`), the docs
  pages those commits touched, and the sites that cite what changed. A full-tree audit is a baseline, not a habit.
- An incremental verifier hunts misses by targeted greps (numbers, Q-numbers, names, history words), not a re-read.
- End each queue item with a light check of the docs and comments it touched, so drift stays small.
- After an interruption, resume the run (`resumeFromRunId`) rather than relaunching it.

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
