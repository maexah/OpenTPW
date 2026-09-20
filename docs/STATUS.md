# Status

Last updated: 2026-09-20 on branch `alexah/94-every-state-answered`, tip `61640a9`.

The tip is the newest `alexah/N` branch and has everything. Confirm with
`git branch -r --sort=-committerdate | head -3`.

## Works

- Lobby: four islands, front end, advisor, weather, particles, options, saves.
- Park: enter from the lobby; ground, paths, queues, placed objects, fixed items, sky, music, weather, camcorder, gadget (2 of 6 buttons).
- People: 13 guests and 5 staff read from the save, drawn, walking, paying at the gate, queueing, boarding.
- Rides: every placed thing runs its script; 71 of 106 opcodes implemented, the rest counted by `Unimplemented`.

## Does not

- No buying, building, hiring, finances, shops serving, litter, saving a park back, video, networking.
- The `meter.wct` mapping behind the happiness gauge is wrong — the last fault Alexah found by playing that is still open.
- 35 opcodes unimplemented. Three README lines and `RideScriptFile.cs:99` still quote older counts.

## Next

`docs/PLAYER-GAPS.md` — the seven gaps a player meets, in the order they meet them. Alexah sets which one
is the goal; one per session.

## Not verified on screen

- **The ride loop completing.** The boarding chain is wired and its arithmetic is covered, but only one
  test method drives it through the production entry point, and no run of the game was made this session.
  Treat "guests ride" as tested, not as confirmed in the game.
- `SpriteScript.ScheduleFrom` and `DropUnreadyNominee`: both are called from `ParkPeople`, and neither is
  pinned by the suite — unwiring either leaves it green. They rest on the decode, not on coverage.
- Staff never enter the cell-occupancy lists (`ParkState.StandOn` is called only from `PeepBehaviour`).

## Numbers

Take counts fresh; these go stale within a day.

| | | measured |
|---|---|---|
| Opcodes | 71 implemented of 106 | 2026-09-20, `case Opcode.` labels vs enum members |
| Tests | 790 total; 379 run without the game, 411 need it | 2026-09-19 review, **not re-run since** |
| Build warnings | 126 (71 are CS8618 nullable) | 2026-09-19 review, **not re-run since** |

## Recent

**2026-09-20 — the memory diet, steps 1 to 5 of `docs/MEMORY-DIET.md`.** Claude Code's project memory
went from **56 files / 1.9 MB to 18 files / 332 KB**. Four commits, no source file touched:

| | |
|---|---|
| `31071c9` | The fourteen rule files become `CLAUDE.md`, which loads in full every session. `CLAUDE.local.md` takes the machine paths and is gitignored. The index drops from 55 lines to 34. |
| `859f577` | **The executable decode moves into `docs/exe/`** — thirteen pages from twenty memory files. This is the one that mattered: those facts existed on one machine, unversioned. Accepted mechanically, 1,042 normalised addresses and 451 `FUN_` names, zero missing. |
| `37decdb` | The two archives and the branch ledger move to `docs/history/`, verbatim and md5-verified. Four workflow traps that lived only in the ledger are lifted into `WORKFLOW.md`. |
| `61640a9` | 1,848 lines of verification postmortems distil to `docs/VERIFYING.md` — 404 lines, one per rule, grouped by symptom. The source's numbering is kept because forty rule numbers are cited from other files. |

**Still owed**, and deliberately not done — these are table rows in the diet, not numbered steps:
`docs/DECISIONS.md` (one paragraph each for the three graphics-migration files), `docs/TOOLING.md`
(the capture and console recipes), the review's phase list, `ride-vm-may-be-replaced.md`, the
docs-clone notes, and the three open-item files. The memory folder still holds all of them.

**Earlier, 2026-09-18.** The live tracker was split: the plan stays in `current-task-progress.md`, its
7,852 lines of dated history became `current-task-archive.md`, now `docs/history/`.
