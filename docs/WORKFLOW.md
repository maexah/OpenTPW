# Workflow

The workflow rules from `AGENTS.md`, with the traps behind them. `AGENTS.md` loads every session and this
file does not, so where the two differ `AGENTS.md` wins. **Pushing**, **The website**, **Weekly builds** and
**Unattended runs** are the maintainer's own procedures; a contributor's route to `main` is a pull request
(`CONTRIBUTING.md`). **Sessions** below is the maintainer's queue routine; a contributor's agent does the task its
person gave it (`AGENTS.md`, "How a task runs"). **Hooks** and **Claims** are for everyone.

## Hooks

`git config core.hooksPath tools/hooks`, once per clone. What runs depends on who you are.

- **Everyone:** `tools/hooks/pre-commit` runs `tools/content_guard.py staged`, which refuses a commit that stages a
  file of the game's: a name on the disc's list (`source/OpenTPW/Client/DiscData.txt`), a type the disc's `data/`
  folder uses, a type the rest of an install has (the executables and libraries, `.vol`, `.icd`, the installer and menu
  files, `.pdf`, `.mpg`, a saved park's `.TPWS` and `.INTS`, `Config.tcf`) or that the wads' members have (`.RSE`,
  `.hmp`, `.sgn` and the other model, sprite and sound types, listed in `ALWAYS_REFUSED_TYPES`), the game's own fonts
  (`fonts.wad`'s TTFs, by name), a disc image, anything in a save folder (any case, any depth, except under `source/`, which holds the `Formats/Save/` readers) or in
  `content/ReferenceScreenshots/`. The weekly build's `tools/weekly/release.py guard` shares only the disc's names and
  types (`disc()`); `EXEMPT`, `ALWAYS_REFUSED_TYPES` and these folder rules are the commit guard's alone. A file of ours that shares a name or type goes on `EXEMPT` in
  the guard. By hand: `python3 tools/content_guard.py staged`, or `paths <name>...`. `tools/hooks/pre-push` does nothing.
- **The maintainer only:** `git config opentpw.maintainer true` makes the same two hooks also run
  `tools/maintainer/hooks/pre-commit` (moves each ticked queue item to `docs/history/queue-done.md` and every old
  "Not verified on screen" account to `docs/history/not-verified.md`, with `tools/maintainer/queue-sweep.py` and
  `status-sweep.py`) and `tools/maintainer/hooks/pre-push` (republishes the website when `main` is pushed). Set it in
  each of the maintainer's clones; without it those sweeps and the website push do not run.
- **The Saturday audit runs** `python3 tools/check_docs_paths.py` (every path, type and link that `README.md`,
  `CONTRIBUTING.md` and `ARCHITECTURE.md` name must still exist) and fixes what it lists, and reads the Quick start's
  steps against the build (`tools/harness/lib/quickstart_check.sh`, `docs/TOOLING.md`).

## Claims

A contributor says which queue item they have taken, so two people do not do the same work.

1. Open an issue, or comment on the one made from the item, saying you will take it. The maintainer adds a last line to
   the item in `docs/QUEUE.md` (a contributor's pull request leaves it alone): `Claimed: @user, #123, 2026-10-28` (your
   GitHub name, the issue or pull request, the date).
2. A claim lapses after 14 quiet days (no commit, comment or pull request on that issue): the maintainer, or the next
   person who asks on the issue, takes the line out.
3. A queue run reads the open issues and pull requests at its start and skips an item with a `Claimed:` line. It
   lists issues and pull requests with `gh` (not on a non-interactive PATH: call it by its full path, such as
   `/home/alex/.local/bin/gh issue list` and `gh pr list`) or reads `git ls-remote upstream 'refs/pull/*/head'`, which
   lists closed pull requests too, so read a pull request's state before treating it as a claim.
4. The maintainer ticks `docs/QUEUE.md` and updates `docs/STATUS.md` when the work is merged; a contributor's pull
   request leaves both alone (the pull request template asks what the work made work, in its own words).

## Branches

- See `AGENTS.md`, Branches. Fast-forward with `git checkout main && git merge --ff-only <branch>`; this
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
- **`gh-pages` on origin is not ours to survey.** It holds the fan website's finished files and nothing of the engine ("The website" below). The website's own publish replaces it by force, the one force push there is. Never check it out, merge it, push to it by hand or delete it, and leave it out of the survey above.
- Push only to `origin` (Alexah's fork), one explicit refspec per branch, no force, no pull request. Run it with `GIT_TERMINAL_PROMPT=0` and the askpass variables unset so a missing login fails fast, then read the tips back with `git ls-remote origin`.
- Before a push, every commit is built **and tested** alone in a throwaway worktree. CI is refused; this ritual is what replaces it.
- Stage files explicitly. Never `git add -A`. Check `git worktree list` and `git branch --list 'worktree-*'` for leftovers from review agents.

Four things about that gate, each of which has cost a session:

- **Put the worktree on real disk, not the session scratchpad.** The scratchpad is a small tmpfs; when it fills, commands exit 0 with truncated output, so the gate appears to pass while measuring nothing.
- **A green working-tree build is not evidence that a commit compiles.** `git mv` stages a rename using the file's *indexed* content, so a class rename can stay unstaged and the commit fails to build even though your tree is fine.
- **`git add -p` is unavailable here.** When one file carries changes belonging to two commits, re-cut the commits so their file sets are *disjoint*; do not try to split a file.
- **A test count in a commit message is a claim about that commit standing alone**, and a full-suite run cannot check it. Either gate the commit alone or do not quote a count.

## The website

The fan website is at https://maexah.github.io/OpenTPW/ . It is its own project: a private repo in the folder
`OpenTPW.Website` beside this one, with its own `CLAUDE.md`, plan and rules. Only two things tie it to this repo
(Alexah, 2026-10-11: "ensure updating the site when relevant is part of the workflow").

- **A push of `main` brings it up to date by itself.** `tools/maintainer/hooks/pre-push` (run by `tools/hooks/pre-push`
  where the clone has `opentpw.maintainer` set, "Hooks" below) runs the website's `tools/publish.sh`. That rebuilds the pages and sends them to the branch
  `gh-pages` of this repo on origin, which GitHub Pages serves. The website's Ride Status board is read from
  `docs/STATUS.md` (the "Numbers" table's `Opcodes` and `Known gaps` rows) and `docs/QUEUE.md` (the open items'
  ids and titles, in order) of the commit being pushed. So those two files are public words: a queue title is
  read by visitors as it is written. The hook never refuses a push. It says so when it did not publish, which
  happens while the website's own repo holds work that is not pushed yet.
- **Some of what the website says is kept by hand, and the task that changes it changes it.**
  Alexah, 2026-10-11: "Then update the website. Don't ask, just do it from now on please." Each land's page
  holds an overview of six fixed systems (Guests, Building, Staff, Breakdowns, Advisor, Saving and finishing),
  one line each, and it never grows (Alexah, 2026-10-13: "I see absolutely no reason to be adding to it like we
  have been"). So the website changes only when a ride, shop or sideshow starts to run for guests, when one of
  those six systems starts to work or works further (a park can be saved, a game can be finished), or when a
  land opens. A new advisor message, button or screen inside a system that already works changes nothing there.
  When it does change, the same session, after the item is merged:
  1. changes the words in the website's repo (its `README.md`, "Changing what the site says", names the place
     for each: the `running` list, that system's one `today` line, the park notice), short and plain, saying
     nothing the engine does not do, and rewording the line, never adding one;
  2. builds it, runs its two tests, looks at the preview's pictures, and has one separate read-only agent run
     the page (the website's rule 12);
  3. commits there and pushes its `main`, which publishes; no yes is asked for this push (the website's rule 1
     names the exception);
  4. reads the live page back (`curl` the page at https://maexah.github.io/OpenTPW/ until it holds the new
     words; GitHub Pages takes a minute or two) and says so in the report.
  This covers the website's words for that task and nothing else: a new page, the look or a screenshot is
  website work with its own yes, and **a push of this repo's `main` still needs its fresh yes** (rule 1).
  Most tasks change nothing there; the Ride Status board follows by itself.

## Weekly builds

An experimental pre-release for Windows, Linux and macOS, made by GitHub from a tag Alexah pushes on a Sunday
(Alexah, 2026-10-27; Intel Macs added the same day). `.github/workflows/weekly.yml` only packages and publishes: it runs no test and decides
nothing about the code, which the local gate has already done. Its one refusal is the content guard: a zip holding
anything that looks like the game's files fails the run, and so does a zip without a file the game or its licences
need (`REQUIRED` in `tools/weekly/release.py`: the program, its libraries and settings, the natives, the shaders, the
font and its licence, the README, `LICENSE.md` and `THIRD-PARTY-NOTICES.md`; `WeeklyReleaseTests` holds it).

- **The contract.** Tag `weekly-YYYY-MM-DD`, release title "Experimental YYYY-MM-DD", a pre-release on origin
  (`maexah/OpenTPW`). Assets `OpenTPW-<tag>-win-x64.zip`, `OpenTPW-<tag>-linux-x64.zip`, `OpenTPW-<tag>-osx-arm64.zip`
  (Apple silicon), `OpenTPW-<tag>-osx-x64.zip` (Intel; both Mac zips labelled untested until a Mac has run them) and
  `SHA256SUMS.txt`, at
  `https://github.com/maexah/OpenTPW/releases/download/<tag>/<asset>`. GitHub's `/releases/latest` skips
  pre-releases, so nothing links it. The release is made as a draft, every asset uploaded and the list checked, and
  published last, so a link never points at a half-built release. A published release is never replaced, and
  every one is kept: none is deleted when a newer one comes (Alexah, 2026-10-27).
- **A week with nothing meaningful gets no release.** The next changelog covers the gap.
- **The Sunday routine**, in order:
  1. Everything is pushed after the usual gate (rule 1's yes, `Pushing` above). Before the tag, read the zip's
     `tools/weekly/README.txt` and the two issue forms against `docs/STATUS.md`: what the README says starts, what
     the build does without a game, and where `console.log` is must still be true of this tag (Q798). Also that the runtime pack the publish takes (`weekly.yml` says 10.0.x) is still the version
     `THIRD-PARTY-NOTICES.md` appendices F and G quote (10.0.12); a later one means reading those two again.
  2. `python3 tools/weekly/changelog.py main` lists the commits since the last `weekly-*` tag (dry-run
     `weekly-test-N` tags are passed over; the first build takes `--from <rev>`). The changelog is written from that
     list, never from memory: broad, plain bullets grouped by area, each may end with its `[abc1234]` link, ending
     with the "Full history" compare link the script prints.
  3. Alexah approves the draft. It is committed as `docs/releases/<tag>.md` (the workflow refuses a tag without it)
     and pushed with `main`, which needs its own yes.
  4. On Alexah's yes, the tag: `git tag weekly-YYYY-MM-DD main && git push origin refs/tags/weekly-YYYY-MM-DD`.
  5. When the run is green, download the Linux zip and run `lib/zipsmoke.py` on it (`docs/TOOLING.md`); read the
     release page back.
  6. In the website's clone, on its `main` with nothing unpushed: `tools/release-site.sh weekly-YYYY-MM-DD`. It waits
     until GitHub shows the release published with all five files (45 minutes at most), then switches the site's
     Download box to it, publishes, reads the live page back and checks each link answers. No yes is needed for
     that one change (the website's `AGENTS.md`, rule 1). If the release never goes live, the site keeps its old
     links. The site shows the build's date and links its release page; it quotes nothing from the changelog.
- **The build's name.** `-p:OpenTPWBuildTag=<tag>` stamps it; every other build is `local`. The SDK adds the commit.
  The console log's first lines (`LaunchReport`) print both, and `console.log` is written with paths cut to
  `<game folder>` and `~` and each player's name to `<player N>` (`SafePaths`), so a tester can attach it as it is.
- **A dry run** is a tag `weekly-test-N`: the same steps with a placeholder note. Delete the release and the tag
  after reading it back.
- **Each zip carries** `tools/weekly/README.txt` (experimental, bring your own game, `--game`, the unsigned
  warnings, Vulkan on Linux, where `console.log` is), `LICENSE.md`, `THIRD-PARTY-NOTICES.md` (a row for every package
  the build restores and every file under `content/`; `ThirdPartyNoticesTests` fails without one, and the licence
  texts in it were read from the packages and the upstream projects, never recalled) and `content/fonts/LICENSE.txt`
  beside the font. Issues come in through `.github/ISSUE_TEMPLATE/weekly-build.yml`, which asks for the zip's name,
  the system and `console.log`; a launch that finds no game writes that file too, in the player's own data folder
  (`FailedStartLogTests`).

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
  RETURNED are kept, and not always those (a finished agent may be run again, and one mid-run is lost whole), so
  put the long agents last and keep each one short enough to lose.
- One verifier per area, with at most about 25 statements to settle. A verifier's cost is its requests times its
  context: one handed two areas made 60 requests and ended at 279k tokens; one handed 236 functions made 72 and ended
  at 369k. Split by area before launch rather than widen one.
- Evidence is split by the script into parts a `Read` takes whole (under about 18k tokens each), and the prompt tells
  the agent to read all of them in one turn. Functions a verifier will need are dumped from Ghidra by the script
  first, with an index from cited address to file.
- A tool call that does not depend on another goes in the same turn; the agent definitions say so.
- Paths an agent reports for a mutation are repo-relative. An absolute one puts the bug in the tree it names, not
  in the copy, and `mutate.py` does not refuse it: check the list before running it.
- After a run, `agents_check.py <transcript dir>` prints each agent's type, model, requests, cache reads, end context
  and any rule it broke.

## Sessions

- **One task per session.** Start Claude Code from the repo root. Read `AGENTS.md` (Claude Code loads it through `CLAUDE.md`) and `docs/STATUS.md`, take the first unticked item in `docs/QUEUE.md` (unless Alexah names another; read the file's head and that item, not the whole queue), then read the one `docs/exe/` page for its area. Check Ghidra answers at the start of work, if you use it (`AGENTS.md` rule 7).
- Keep a multi-step task's checklist in the live plan and tick each step before moving on (rule 16).
- `docs/STATUS.md`'s "Not verified on screen" holds the item just landed and no other. Write a new item's account above the one before it; the maintainer's pre-commit hook runs `tools/maintainer/status-sweep.py`, which moves every earlier account to `docs/history/not-verified.md`.
- End the session when the task is committed, its `docs/QUEUE.md` item ticked and `docs/STATUS.md` updated, and what the item made work in `docs/WORKS.md` (STATUS's one line per system changes only when a system moves; the item's "not verified" account goes above the last one's). The maintainer's pre-commit hook (`tools/maintainer/hooks/pre-commit`, see "Hooks") runs `tools/maintainer/queue-sweep.py`, which moves every ticked item to `docs/history/queue-done.md` and stages both files; do not move one by hand. Do not carry the next task in the same context. When Alexah says a clear is coming, read `docs/QUEUE.md` and show the next five unticked items with what each asks (rule 17).
- Memory files hold rules and the live plan only, each under 300 lines, and nothing that is finished. Facts go in `docs/`. Corrections replace old text.

## Unattended runs

Alexah, 2026-10-09: the queue may be worked with nobody at the desk, one fresh session per item, committing locally
until told to stop. The overnight run of 2026-10-06 was fifteen items in one session with no plan; this is the plan.

- **Alexah starts a run; a session never starts one on its own.** `autorun.py start N` (`CLAUDE.local.md` says where
  it lives) checks both clones, the desktop and Ghidra, then starts up to N headless sessions one after another, each
  with an empty context, the same model and effort, and the permission mode a session at the desk has. `status` shows
  it, `stop` ends it after the item in hand, `abort` ends it now, and `smoke` is one short session that touches
  nothing: run it after Claude Code or the script changes.
- **A run reads the open issues and pull requests at its start** and skips a queue item that has a `Claimed:` line ("Claims" above).
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
  session's process ends, which a clear does not do. A headless
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
  This repo's line also links the fan website (Alexah, 2026-10-11).
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
- **At the gate, keep every failing test's name** (`grep -E '^\s+Failed '` over the whole output, never a tail), and never put the merge in the same command after the test: a failure whose name is lost cannot be chased, and a chained merge lands whatever the test said.
- **Undo a hand-made bug with `git checkout -p` or `git diff`, never with `sed` on a short pattern**, which can match a second line; or put the bug back through `mutate.py --only`.
- **A test's "other" value is written as a constant**, neither nought nor the real one: made by arithmetic on the real one (`held ^ 0x10000`) it can come out as the empty value, and a put-back bug lives on that.
- **Keep a capture of the original until every instrument planned for it has been tried.**
- **A script that appends a line to a doc checks the file's last byte:** split on newlines and added after the last, empty piece, the line goes in without the final newline.
- **A record a stranger can repeat.** A new item's confirmation is written as four things: the **prediction** (made
  before the run), the **command** or clicks and the build, the census or **log lines** it printed, and the **result**
  (held, or missed with its cause). Screenshots are welcome where they really help; attach one to the pull request
  or issue, because a committed picture stays in the history after it is removed. The game's raw textures and models
  are never committed. `CONTRIBUTING.md`, "Showing how you checked", gives the contributor's version.
- Say what was **not** verified.
