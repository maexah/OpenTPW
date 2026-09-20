# Memory diet

What to do with each of the 56 files in `~/.claude/projects/-home-alex-repos-OpenTPW/memory/`.

Today the folder is 1.85 MB, about 462,000 tokens. The index tells a session to read about 125,000 tokens of it before opening any code. That is more than half of a 200,000-token window, and it is why sessions compact and then forget.

The aim: the memory folder holds **rules and the live plan only**, under 40 KB in total. Everything else moves into the repo (versioned, backed up, readable by humans and by any session) or into git history.

## Where each kind of content goes

| Kind | Goes to | Why |
|---|---|---|
| Rules and preferences | `CLAUDE.md` (committed) | Loaded every session, in full, unconditionally. |
| Machine paths, capture recipes | `CLAUDE.local.md` (not committed) | Machine-specific. |
| Live plan | `memory/current-task-progress.md`, under 300 lines | The one thing that must survive compaction. |
| Executable knowledge (addresses, state machines, field tables) | `docs/exe/<area>.md` in the repo | 271 addresses and 397 function names exist only in memory today. Unversioned, on one machine. |
| File-format knowledge | The FileFormats docs clone (already the rule) | |
| Finished work, per-branch ledgers, old plans | Git history and the commit messages | Already there. Keeping a second copy in memory doubles the reading and invites grep hits from stale text. |
| Lessons about verification | `docs/VERIFYING.md`: one line per lesson, 85 lines | The current 158 KB file is unreadable in one sitting and the index admits its count is unreliable. |

## File by file

| File | Size | Verdict |
|---|---|---|
| `MEMORY.md` | 13 KB | Rewrite to under 40 lines. One line per surviving file. No corrections of itself. |
| `current-task-progress.md` | 29 KB | Keep. Trim to the plan. Move every "this said X until date" sentence out. |
| `current-task-archive.md` | 652 KB | Move out of the memory folder entirely (to `docs/history/` or delete; git has it). Its 449 addresses go to `docs/exe/` first. |
| `project-history-archive.md` | 81 KB | Same as above. |
| `contribution-branch-layout.md` | 155 KB | Keep the first 30 lines (the layout and its reason) in `docs/WORKFLOW.md`. The per-branch ledger is the git log; drop it. |
| `verify-every-ordering.md` | 158 KB | Distil to `docs/VERIFYING.md`, one line per rule. Keep the three or four with a worked example. |
| `park-data-layout.md` | 133 KB | Split: file-format parts to the FileFormats docs (most is already there), the rest to `docs/exe/park.md`. |
| `park-engine-from-exe.md` | 60 KB | `docs/exe/park-engine.md`. |
| `park-ride-operation.md` | 90 KB | `docs/exe/ride-operation.md`. Remove the eleven "still additive" update paragraphs. |
| `park-hud-from-exe.md` | 37 KB | `docs/exe/hud.md`. |
| `park-weather-from-exe.md` | 31 KB | `docs/exe/weather.md`. |
| `park-advisor-from-exe.md` | 19 KB | `docs/exe/advisor.md`. |
| `original-boot-sequence.md`, `original-lobby-park-sharing.md`, `original-positional-audio.md`, `original-render-states.md`, `original-save-files.md` | 35 KB | `docs/exe/boot.md`, `scenes.md`, `audio.md`, `render-states.md`, `saves.md`. |
| `lobby-*.md`, `advisor-*.md`, `loading-screen.md`, `esc-menu-and-settings.md`, `theme-names-and-locale-tables.md` | 50 KB | `docs/exe/lobby.md` and `docs/exe/ui.md`. Trim "built on branch N" history. |
| `opentpw-fileformats-docs.md` | 103 KB | Keep 20 lines (where the clone is, what belongs there, how to build it) in `docs/README.md`. The rest is per-session history. |
| `ride-vm-may-be-replaced.md` | 15 KB | Reduce to the four-line decision in `CLAUDE.md` rule 3. Delete the file. |
| `codebase-review-and-plan.md` | 10 KB | Keep the phase list (A, B, E, F unbuilt) in `docs/STATUS.md`. The artifact links go there too. |
| `neoveldrid-migration-plan.md`, `post-neoveldrid-audit.md`, `veldrid-arm64-and-alternatives.md` | 55 KB | Done work. `docs/DECISIONS.md` gets one paragraph each. Delete the files. |
| `lobby-finishing-plan.md`, `dead-settings-to-revisit.md`, `keep-loading-steps-current.md` | 15 KB | Open items to `docs/STATUS.md`. Delete the files. |
| `ask-before-github.md`, `no-tooling-in-the-codebase.md`, `dead-code-and-in-game-proof.md`, `think-deeply-by-default.md`, `modular-data-driven-engine.md`, `no-framerate-dependent-motion.md`, `usability-over-empty-original-text.md`, `fix-tools-dont-work-around.md`, `no-connector-reminders.md`, `test-runs-are-audible.md`, `workflow-agents-share-the-tree.md`, `track-progress-externally.md`, `ghidra-mission-critical.md`, `opentpw-format-goal.md` | 45 KB | These are the rules. They are now `CLAUDE.md`. Delete the files once it is in place. |
| `ghidra-headless.md`, `opentpw-local-launch.md`, `opentpw-debug-console.md`, `verifying-rendering-by-capture.md`, `verifying-audio-by-capture.md`, `identifying-speech-by-transcription.md`, `audio-levels-from-measurement.md` | 65 KB | Machine and tooling recipes. `CLAUDE.local.md` (paths) and `docs/TOOLING.md` (recipes). |

## Rules for what stays in memory

- The index is under 40 lines. Each line names a file and says in ten words when to read it.
- No file over 300 lines. If one grows past that, something in it is finished and belongs in `docs/` or git.
- A correction replaces the text it corrects. No `>>>` markers, no "this line said", no dated trims.
- A memory file never quotes a count (tests, warnings, opcodes). Counts are taken fresh.
- Memory is keyed to the working directory. Always start Claude Code from the repo root.

## Order of work

1. Put `CLAUDE.md` and `CLAUDE.local.md` in place. Delete the fourteen rule files.
2. Rewrite `MEMORY.md` to the short form.
3. Move the `*-from-exe` and `original-*` files into `docs/exe/`, trimming history as they go. This is the step that saves the 271 addresses.
4. Move the two archives and the branch ledger out of the memory folder.
5. Distil `verify-every-ordering.md`.

Each step is one commit and one short session.
