# Memory diet

What to do with each of the files in `~/.claude/projects/-home-alex-repos-OpenTPW/memory/`, and whether it is done.

**The sizes in the tables are the diet's STARTING POINT, not today's.** It began at 56 files and 1.85 MB; on
2026-09-24 the folder is eight files, every one under 300 lines. The Status column says which rows are done and what
is still owed. Measure the folder with `wc -l`; do not quote a figure from here.

When this was written the folder was 1.85 MB, about 462,000 tokens. The index told a session to read about 125,000
tokens of it before opening any code. That was more than half of a 200,000-token window, and it is why sessions
compacted and then forgot.

The aim: the memory folder holds **rules and the live plan only**, under 40 KB in total. Everything else moves into the
repo (versioned, backed up, readable by humans and by any session) or into git history.

## Where each kind of content goes

| Kind | Goes to | Why |
|---|---|---|
| Rules and preferences | `CLAUDE.md` (committed) | Loaded every session, in full, unconditionally. |
| Machine paths, capture recipes | `CLAUDE.local.md` (not committed) | Machine-specific. |
| Live plan | `memory/current-task-progress.md`, under 300 lines | The one thing that must survive compaction. |
| Executable knowledge (addresses, state machines, field tables) | `docs/exe/<area>.md` in the repo | Versioned and on more than one machine. Done 2026-09-20 (`859f577`). |
| File-format knowledge | The FileFormats docs clone (already the rule) | |
| Finished work, per-branch ledgers, old plans | `docs/history/`, verbatim, plus git history | Versioned and kept whole; grep it, never read it whole. `history/README.md` says why the ledgers were kept. |
| Lessons about verification | `docs/VERIFYING.md`, numbered rules grouped by symptom (the diet's target was one line per lesson, 85 lines) | Done 2026-09-20 (`61640a9`); the 158 KB source is in the memory backups folder. |
| Open work | `docs/QUEUE.md` | Every open item is a queue item, so a session finds it by taking the queue. |

## File by file

| File | Size | Verdict | Status |
|---|---|---|---|
| `MEMORY.md` | 13 KB | Rewrite to under 40 lines. One line per surviving file. No corrections of itself. | Done 2026-09-24. |
| `current-task-progress.md` | 29 KB | Keep. Trim to the plan. Move every "this said X until date" sentence out. | Done 2026-09-24: the plan, the push state to verify, and Alexah's standing instructions. |
| `current-task-archive.md` | 652 KB | Move out of the memory folder entirely (to `docs/history/` or delete; git has it). Its 449 addresses go to `docs/exe/` first. | Done 2026-09-20 (`37decdb`). |
| `project-history-archive.md` | 81 KB | Same as above. | Done 2026-09-20 (`37decdb`). |
| `contribution-branch-layout.md` | 155 KB | Keep the first 30 lines (the layout and its reason) in `docs/WORKFLOW.md`. The per-branch ledger is the git log; drop it. | Done 2026-09-20 (`37decdb`), differently: moved whole to `docs/history/` and kept (`history/README.md` says why); its four gate traps are in `docs/WORKFLOW.md`. |
| `verify-every-ordering.md` | 158 KB | Distil to `docs/VERIFYING.md`, one line per rule. Keep the three or four with a worked example. | Done 2026-09-20 (`61640a9`). |
| `park-data-layout.md` | 133 KB | Split: file-format parts to the FileFormats docs (most is already there), the rest to `docs/exe/park.md`. | Done 2026-09-20 (`859f577`), moved whole: its format half is Part 1 of `docs/exe/park.md`, duplicated in the FileFormats clone and marked so. |
| `park-engine-from-exe.md`, `park-ride-operation.md`, `park-hud-from-exe.md`, `park-weather-from-exe.md`, `park-advisor-from-exe.md` | 237 KB | `docs/exe/park-engine.md`, `ride-operation.md`, `hud.md`, `weather.md`, `advisor-park.md`. | Done 2026-09-20 (`859f577`). |
| `original-boot-sequence.md`, `original-lobby-park-sharing.md`, `original-positional-audio.md`, `original-render-states.md`, `original-save-files.md` | 35 KB | `docs/exe/boot.md`, `scenes.md`, `audio.md`, `render-states.md`, `saves.md`. | Done 2026-09-20 (`859f577`). |
| `lobby-*.md`, `advisor-*.md`, `loading-screen.md`, `esc-menu-and-settings.md`, `theme-names-and-locale-tables.md` | 50 KB | `docs/exe/lobby.md` and `docs/exe/ui.md`. Trim "built on branch N" history. | Done 2026-09-20 (`859f577`). |
| `opentpw-fileformats-docs.md` | 103 KB | Keep 20 lines (where the clone is, what belongs there, how to build it) in `docs/README.md`. The rest is per-session history. | Done 2026-09-24: the rules are `docs/README.md`, "The FileFormats docs clone"; the paths are `CLAUDE.local.md`; the ledger is `docs/history/fileformats-docs-ledger.md`; its open errors are `QUEUE.md` Q81. |
| `ride-vm-may-be-replaced.md` | 15 KB | Reduce to the four-line decision in `CLAUDE.md` rule 3. Delete the file. | Done 2026-09-24: the decision is `CLAUDE.md` rule 3 and "Words"; its plan's link is in `docs/history/README.md`. |
| `codebase-review-and-plan.md` | 10 KB | Keep the phase list (A, B, E, F unbuilt) in `docs/STATUS.md`. The artifact links go there too. | Done 2026-09-24, to the queue instead of STATUS: its open work went to `QUEUE.md` Q70-Q75, its artifacts and decisions are in `docs/history/README.md`, its data path in `CLAUDE.local.md`. |
| `neoveldrid-migration-plan.md`, `post-neoveldrid-audit.md`, `veldrid-arm64-and-alternatives.md` | 55 KB | Done work. `docs/DECISIONS.md` gets one paragraph each. Delete the files. | Done 2026-09-24: `docs/DECISIONS.md`; two method traps became `VERIFYING.md` rules 122 and 123. |
| `lobby-finishing-plan.md`, `dead-settings-to-revisit.md`, `keep-loading-steps-current.md` | 15 KB | Open items to `docs/STATUS.md`. Delete the files. | Done 2026-09-24, to the queue instead of STATUS: the lobby plan's open items are `QUEUE.md` section G and Q32; the dead options rows and the loading-step seeds are in `docs/exe/ui.md`. |
| `ask-before-github.md`, `no-tooling-in-the-codebase.md`, `dead-code-and-in-game-proof.md`, `think-deeply-by-default.md`, `modular-data-driven-engine.md`, `no-framerate-dependent-motion.md`, `usability-over-empty-original-text.md`, `fix-tools-dont-work-around.md`, `no-connector-reminders.md`, `test-runs-are-audible.md`, `workflow-agents-share-the-tree.md`, `track-progress-externally.md`, `ghidra-mission-critical.md`, `opentpw-format-goal.md` | 45 KB | These are the rules. They are now `CLAUDE.md`. Delete the files once it is in place. | Done 2026-09-20 (`31071c9`). |
| `ghidra-headless.md`, `opentpw-local-launch.md`, `opentpw-debug-console.md`, `verifying-rendering-by-capture.md`, `verifying-audio-by-capture.md`, `identifying-speech-by-transcription.md`, `audio-levels-from-measurement.md` | 65 KB | Machine and tooling recipes. `CLAUDE.local.md` (paths) and `docs/TOOLING.md` (recipes). | Half done: `opentpw-local-launch.md` is retired into `CLAUDE.local.md` (2026-09-24), and the other six were cut to current method notes; **`docs/TOOLING.md` is not written**. |

## Rules for what stays in memory

- The index is under 40 lines. Each line names a file and says in ten words when to read it.
- No file over 300 lines. If one grows past that, something in it is finished and belongs in `docs/` or git.
- A correction replaces the text it corrects. No `>>>` markers, no "this line said", no dated trims.
- A memory file never quotes a count (tests, warnings, opcodes). Counts are taken fresh.
- Memory is keyed to the working directory. Always start Claude Code from the repo root.

## Order of work

1. Put `CLAUDE.md` and `CLAUDE.local.md` in place. Delete the fourteen rule files. Done 2026-09-20 (`31071c9`).
2. Rewrite `MEMORY.md` to the short form. Done 2026-09-24.
3. Move the `*-from-exe` and `original-*` files into `docs/exe/`, trimming history as they go. Done 2026-09-20
   (`859f577`).
4. Move the two archives and the branch ledger out of the memory folder. Done 2026-09-20 (`37decdb`).
5. Distil `verify-every-ordering.md`. Done 2026-09-20 (`61640a9`).
6. Write `docs/DECISIONS.md` from the three graphics notes. Done 2026-09-24.
7. Move the open-item files' work into the queue. Done 2026-09-24.
8. Write `docs/TOOLING.md` from the recipe notes, and `docs/ARCHITECTURE.md`. **Outstanding.**

Each step is one commit and one short session.
