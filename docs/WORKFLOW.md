# Workflow

The workflow rules from `CLAUDE.md`, with the traps behind them. `CLAUDE.md` loads every session and this
file does not, so where the two differ `CLAUDE.md` wins.

## Branches

- See `CLAUDE.md`, Branches. Fast-forward with `git checkout main && git merge --ff-only <branch>`; this
  sandbox refuses `git branch -f main`.
- Name a branch for what it changes, not with a phrase.
- **A merged task branch is deleted, on origin and locally** (Alexah, 2026-10-03): once it is inside `main` and
  `main` is pushed, it holds nothing `main` does not. Keep only the newest, so the next branch number can still be
  read from `git branch -a`. Delete with `git branch -d`, which refuses a branch that is not merged, and check
  `git merge-base --is-ancestor <tip> <server main>` before deleting one from origin. A local branch left behind
  would go back up at the next push. `alexah/162-audit-open-questions` is kept: it is not merged.
- The FileFormats clone runs the same way with `master` in place of `main`: a task's docs go on a branch off `master`,
  fast-forwarded into `master` when OpenTPW's `main` is, and pushed together (Alexah, 2026-09-29).

## Pushing

- **Never push, and never open a pull request, without a fresh yes from Alexah.** Commit locally as work finishes. Say what is ready, which branches, and where it would go. Then wait.
- When the yes comes, it covers everything of Alexah's that is not yet up. Survey every local branch in **both** clones against origin with `git ls-remote`, not a remembered list, and do not narrow it to what you named. Two stay behind unless asked for by name: a branch carrying an open pull request, and `wf-review-*` scratch branches. `git ls-remote upstream 'refs/pull/*'` lists closed pull requests too, so read a pull request's state (`https://api.github.com/repos/<owner>/<repo>/pulls/<n>`, no login needed) before holding its branch back.
- Push only to `origin` (Alexah's fork), one explicit refspec per branch, no force, no pull request. Run it with `GIT_TERMINAL_PROMPT=0` and the askpass variables unset so a missing login fails fast, then read the tips back with `git ls-remote origin`.
- Before a push, every commit is built **and tested** alone in a throwaway worktree. CI is refused; this ritual is what replaces it.
- Stage files explicitly. Never `git add -A`. Check `git worktree list` and `git branch --list 'worktree-*'` for leftovers from review agents.

Four things about that gate, each of which has cost a session:

- **Put the worktree on real disk, not the session scratchpad.** The scratchpad is a small tmpfs; when it fills, commands exit 0 with truncated output, so the gate appears to pass while measuring nothing.
- **A green working-tree build is not evidence that a commit compiles.** `git mv` stages a rename using the file's *indexed* content, so a class rename can stay unstaged and the commit fails to build even though your tree is fine.
- **`git add -p` is unavailable here.** When one file carries changes belonging to two commits, re-cut the commits so their file sets are *disjoint*; do not try to split a file.
- **A test count in a commit message is a claim about that commit standing alone**, and a full-suite run cannot check it. Either gate the commit alone or do not quote a count.

## Codex

Set by Alexah 2026-10-06. Codex is the second developer, reached through the `codex-worker` bridge; the lead may
assign to it without asking and may change the bridge when the work needs it. Its tokens are not unlimited: the
subagent rules below hold for it, stage by stage.

**Model and effort by stage.** Every `assign` names both; `models` lists the pairs the runtime offers, and there is
no fallback, so a pair the account lacks fails the task.

| Stage | Model, effort | Why |
|---|---|---|
| A connectivity check, one or two bridge calls | `gpt-6-astra`, low | About 16k tokens a call, measured 2026-10-04 |
| Gathering and first-pass sweeps whose every finding is then verified | the catalogue's smaller model (`gpt-6-luna`), low or medium | A verifier checks the result. Not yet used here: read the first run's cost and quality from `status` and write them in this row |
| An un-anchored second read of a commit, a refutation hunt | `gpt-6-astra`, high | About 1.2 million tokens a commit, nine tenths cached reads (two runs, 2026-10-04 and 2026-10-06) |
| A code change | `gpt-6-astra`, high or above | The bridge refuses anything less |
| `xhigh`, `max`, `ultra` | only when high has failed to settle a question that blocks the work, said before it is launched | The same rule as Fable's |

**What it cannot do.** A worker here has no shell: no build, no test, no game, no Ghidra. Decoding, verifying by
running and anything in Ghidra go to `tpw-verify` or stay with the lead.

**When.** The second read of an item's own diff is assigned at its work-in-progress commit, before the game runs
and the bugs are put back, so it reads while they run; assigned after them it only adds to the wait.

**Budget.** One or two assignments a task at the most, and none for what a grep settles. Each names one commit,
the narrow `paths` it needs and a `timeout_seconds`; one review of 2026-10-06 ran into the task's runtime limit with
nothing handed back. Read `status` after a run for its `token_usage`, as `bytype.py` is read after a workflow.

**Its findings are leads.** Each is checked first-hand before it is acted on, a cheaper model's by the lead or by
Astra, never by itself. Nothing personal and no machine path goes into an assignment: its text and its worktree's
files leave the machine.

## Subagents

Set by Alexah 2026-09-26, after one staleness audit spent about 40M subagent tokens and hit the weekly limit. None of
this removes a check: the adversarial verify and the review of applied edits stay.

**Model by stage.** Every agent names its type: `tpw-gather` (Haiku), `tpw-sweep` (Sonnet, high effort) or
`tpw-verify` (Opus, high effort), in `.claude/agents/`. In a workflow script that is `agentType: 'tpw-gather'` on every
`agent()` call; with the Agent tool it is `subagent_type`. **An agent with no type is not matched to its stage**: every
workflow agent to 2026-10-01 ran untyped, and almost all of them ran Opus whatever they were handed. A hook on this
machine (`.claude/hooks/workflow-agent-type.py`) refuses a workflow script with an untyped `agent()` call. After a run,
`bytype.py` prints its agents by type and model; a `workflow-subagent` row is a call that left the type off.

| Stage | Model | Why |
|---|---|---|
| Greps, listing files, headings or `case` labels, gathering evidence for a named question, formatting JSON or text | Haiku | A script checks the result |
| First-pass audit of a file, triaging notes, merging edits that agree, drafting queue items from verified findings, read-only sweeps | Sonnet | An Opus verifier checks every finding |
| Adversarial verify, reviewing applied edits, anything in Ghidra, decoding, merges that disagree on a fact, code changes | Opus | A wrong answer here costs a session |

A cheaper finder is safe only under an Opus verifier that also hunts misses; never Sonnet verifying Sonnet.

**Opus does not do the lookups.** Ahead of each verify stage, a `tpw-gather` stage (or the script itself) runs the
greps and listings the verifier will need and writes them to one evidence file per agent. The verifier is handed that
path, judges, and does the Ghidra work. It still greps when it has to chase something the evidence does not cover.

**Calls.** Every call re-reads the agent's whole context, so an agent costs its calls times its context; the results
themselves are small. Batch them: several greps in one command, several addresses in one Ghidra `run_python`.
`tpw-gather` reports what is left at about 25 calls and `tpw-sweep` at about 30 (measured 2026-10-04: sweeps handed
their evidence finished in 7 to 14); both numbers are reset from measured runs at the weekly usage look. **`tpw-verify` has no limit**: a check is never thinned to save
tokens (Alexah, 2026-10-03).

**Fable is a last resort.** Its tokens count against the weekly limit and the Fable limit at the same time, so it
never saves budget. Use it only when Opus at high effort has failed to settle a question that blocks the work, and
say so before launching it.

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
- After an interruption, resume the run (`resumeFromRunId`) rather than relaunching it. Only agents that had
  RETURNED are kept, and not always those (2026-10-04: two of six finished sweeps came back from the cache, and four
  verifiers mid-run were lost whole), so put the long agents last and keep each one short enough to lose.
- One verifier per area, with at most about 25 statements to settle. A verifier's cost is its requests times its
  context: one handed two areas made 60 requests and ended at 279k tokens; one handed 236 functions made 72 and ended
  at 369k. Split by area before launch rather than widen one.
- Evidence is split by the script into parts a `Read` takes whole (under about 18k tokens each), and the prompt tells
  the agent to read all of them in one turn. Functions a verifier will need are dumped from Ghidra by the script
  first, with an index from cited address to file.
- A tool call that does not depend on another goes in the same turn. The verifiers of 2026-10-04 sent one tool a
  request; the agent definitions now say to send them together.
- Paths an agent reports for a mutation are repo-relative, and the runner refuses an absolute one: on 2026-10-04
  absolute paths put twenty mutations in the main tree instead of the worktree.
- After a run, `agents_check.py <transcript dir>` prints each agent's type, model, requests, cache reads, end context
  and any rule it broke.

## Sessions

- **One task per session.** Start Claude Code from the repo root. Read `CLAUDE.md` (automatic) and `docs/STATUS.md`, take the first unticked item in `docs/QUEUE.md` (unless Alexah names another; read the file's head and that item, not the whole queue), then read the one `docs/exe/` page for its area. Check the Ghidra headless server at the start of work (`CLAUDE.md` rule 7).
- Keep a multi-step task's checklist in the live plan and tick each step before moving on (rule 16).
- `docs/STATUS.md`'s "Not verified on screen" holds the item just landed and no other. Write a new item's account above the one before it; the same pre-commit hook runs `tools/status-sweep.py`, which moves every earlier account to `docs/history/not-verified.md`.
- End the session when the task is committed, its `docs/QUEUE.md` item ticked and `docs/STATUS.md` updated. The pre-commit hook (`tools/hooks/pre-commit`, enabled once per clone with `git config core.hooksPath tools/hooks`) runs `tools/queue-sweep.py`, which moves every ticked item to `docs/history/queue-done.md` and stages both files; do not move one by hand. Do not carry the next task in the same context. When Alexah says a clear is coming, read `docs/QUEUE.md` and show the next five unticked items with what each asks (rule 17).
- Memory files hold rules and the live plan only, each under 300 lines, and nothing that is finished. Facts go in `docs/`. Corrections replace old text.

## Unattended runs

Alexah, 2026-10-09: the queue may be worked with nobody at the desk, one fresh session per item, committing locally
until told to stop. The overnight run of 2026-10-06 was fifteen items in one session with no plan; this is the plan.

- **Alexah starts a run; a session never starts one on its own.** `autorun.py start N` (`CLAUDE.local.md` says where
  it lives) checks both clones, the desktop and Ghidra, then starts up to N headless sessions one after another, each
  with an empty context, the same model and effort, and the permission mode a session at the desk has. `status` shows
  it, `stop` ends it after the item in hand, `abort` ends it now, and `smoke` is one short session that touches
  nothing: run it after Claude Code or the script changes.
- **A session inside a run** sees `OPENTPW_AUTORUN=1` and is told what `prompt.md` beside the script says: one item,
  every rule, nothing trimmed. Its last line is `DONE <Qn>`, `BLOCKED <Qn>`, `STOPPED <reason>` or `NOTHING-LEFT`.
- **An item that needs Alexah is skipped, never guessed:** a decision that is theirs, their eyes or ears, or an item
  that follows one not done. The session adds `**Needs Alexah (<date>):** <the question>` as the item's last line and
  takes the next item it can finish alone. An item that turns out to need them halfway stays on its branch, unmerged,
  with the same line naming the branch. The session Alexah answers takes the line out.
- **Nothing is pushed.** Rule 1 stands. In a run three guards hold it (a hook, a deny rule, and origin's push address
  pointed at a path that does not exist for everything the session runs), and the script reads origin before and
  after each session.
- **The script believes git over the session.** It ends the run on: no last line (a crash, the usage limit, the
  five-hour limit), a clone left dirty, off its branch, with a new untracked file or an extra worktree, origin moved,
  `DONE` said with `main` where it was, two `BLOCKED` running, or a `STOP` file. A session writes `STOP` when a tool
  is broken (rule 7); `autorun.py clear` removes it once the tool is mended.
- **While a run is live, every other session leaves the repo alone:** no build, no test, no git, no game, no Ghidra
  call. Looking is fine (`autorun.py status`, `report.md`).
- **Ghidra's lock is the trap.** A session's Ghidra server takes the project at its first call and keeps it until that
  session's process ends, which a clear does not do (on 2026-10-09 one had held it for three days). A headless
  session then finds the project locked. Before a run, the session at the desk calls `release_project` and makes no
  Ghidra call until the run is over; the script probes Ghidra before every session and names the holder's remedy.
- **A headless session is kept open, or it dies at its first wait.** Started with its prompt as a plain argument,
  `claude -p` ends at the first end of turn and kills the background commands it started (measured 2026-10-09: out at
  9 s of a 20 s `sleep`), where a session at the desk is woken when they finish. The script starts it with
  `--input-format stream-json` and keeps the input open: a finished background command (woken at 22 s) or agent then
  wakes it the same way. The session is over at the turn that holds its last line as a whole line; a turn that ends
  with no last line and nothing running is nudged after two minutes, three times at most. `AskUserQuestion` does not
  exist in a headless session.
- Each session appends its account to `report.md` beside the script, and the script adds its own line under it: the
  last line, both tips before and after, the hours, and the id `claude --resume` takes.

## Commits

- Small. One behaviour per commit.
- The message says **why**, and what **evidence** was gathered: the test names, the debug-console run, the addresses traced, the number predicted and the number seen.
- Executable facts go into `docs/exe/` in the same commit; file-format facts go into the FileFormats docs clone (a separate repo) in the same session. A fact only in a commit message or a memory file is a fact the next session will not find.
- Credit outside work that led to a fact. When another project pointed at it first (Aluzed's OpenTPW-decomp fork, reviewed 2026-09-30, Q185-Q197), the message carries `Lead: Aluzed's OpenTPW-decomp fork (github.com/aluzed/OpenTPW-decomp, <ticket or commit>); established here by <the addresses traced or the data measured>`, and the docs page that records the fact says in one line that the fork pointed at it first. Code adapted from it keeps its MIT notice in a header line and carries an `Adapted-from: aluzed/OpenTPW-decomp@<commit> (Aluzed, MIT)` trailer. A fact the review found with no lead from the fork needs no fork credit.

## READMEs

The two READMEs (this repo's and the FileFormats clone's) are written for players and newcomers (Alexah, 2026-10-02).

- **One layout.** Centred title, a one-line tagline, then a line linking both repos with the current one in bold.
  Sections in this order: What this is, how far along it is, Quick start, the repo's own sections, Contributing, License.
- **One voice.** Speak to the reader as "you", in short, plain, present-tense sentences. Say what to do, not how it
  came about: no history, no storytelling. The same thing is said in the same words in both files.
- **Simple first.** Write the main text for someone who has never used a terminal: numbered steps, everyday words.
  Technical detail and terms (Joliet, environment variables) go in a `<details>` block titled "For technical users".
- **Nothing that goes stale.** No counts, dates or test numbers; link `docs/STATUS.md` instead. A list kept by hand
  (the FileFormats page table) is named in the steps for adding to it.
- **Look at it rendered,** at phone width too, before asking to push.

## Verifying

- Build with `--no-incremental`, then `dotnet test --no-build` with `OPENTPW_GAME_PATH` set; without it the game-data tests skip, so read the skip count, not just "Passed!". Take the counts fresh; never compare against a remembered number.
- Confirm in the running game, launched with `--game` or `OPENTPW_GAME_PATH`, with `OPENTPW_DEBUG_CONSOLE=1` and `SDL_AUDIODRIVER=dummy`. Predict the number before observing it.
- Put the bug back and re-run. If the suite stays green, the test is hollow: extract the decision into a pure function and pin that. An item's bugs go in a list for `mutate.py`, which checks every pattern, proves each copy of the tree green unchanged, and runs eight copies at once (61 bugs in 9.4 minutes, 2026-10-09); a bug that does not compile was never run and counts for nothing. `stryker.sh` adds Stryker.NET's mechanical bugs on the files an item changed (a comparison turned, a statement dropped): it finds the boundary nobody thought of and does not replace the list, which holds the bugs that mean something.
- A run that needs no photograph (a control, a census) can go on a private display beside the one on the desktop (`CLAUDE.local.md`, "Off-screen runs"). Its pictures are not evidence: the software driver draws every texture white.
- Before predicting a number, grep the earlier runs' logs for the same scene. A figure measured under other conditions (a load of one, a load of thirteen) is not this scene's.
- **Run the scenes side by side.** An off-screen run and a desktop run use separate game folders, so start the census-only scenes off-screen while the photographed one is on the desktop; a second file goes into the original that is already running (`loadfile.sh`).
- **While iterating, run the tests the change touches** (`--filter`); the whole suite is run once, at the gate.
- **Edit source with the Edit tool, one change a call.** A batch of replacements in one script fails whole on one miscounted tab and costs a rebuild.
- Say what was **not** verified.
