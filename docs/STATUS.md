# Status

Last updated: 2026-09-20 on branch `alexah/94-every-state-answered` (tip, `fe6964e`).

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

## Not verified on screen

- **The ride loop completing.** The boarding chain is wired and its arithmetic is covered, but only
  one test method drives it through the production entry point, and no run of the game was made this
  session. Treat "guests ride" as tested, not as confirmed in the game.
- `SpriteScript.ScheduleFrom` and `DropUnreadyNominee`: both are called from `ParkPeople`, and neither
  is pinned by the suite — unwiring either leaves it green. They rest on the decode, not on coverage.
- Staff never enter the cell-occupancy lists (`ParkState.StandOn` is called only from `PeepBehaviour`).

## Numbers

Take counts fresh; these go stale within a day.

| | | measured |
|---|---|---|
| Opcodes | 71 implemented of 106 | 2026-09-20, `case Opcode.` labels vs enum members |
| Tests | 790 total; 379 run without the game, 411 need it | 2026-09-19 review, not re-run since |
| Build warnings | 126 (71 are CS8618 nullable) | 2026-09-19 review, not re-run since |

## Recent

**2026-09-20 — memory diet, step 1 of `docs/MEMORY-DIET.md`.** `CLAUDE.md` checked against the
fourteen rule files in Claude Code's memory folder: thirteen were already covered. One was not, and
was added as rule 16 (write the plan down as you go, so a compaction loses nothing). Rule 1 was
completed with the two standing push exceptions. `CLAUDE.local.md` created from the machine notes and
added to `.gitignore`. The memory index rewritten from 55 lines to 34, one entry per surviving file.
The fourteen rule files deleted from the memory folder — they are now these rules. Steps 2 to 5 of
the diet are untouched; the executable decode still lives only in the memory folder.

**Earlier, 2026-09-18.** The live tracker was split: the plan stays in `current-task-progress.md`,
its 7,852 lines of dated history moved to `current-task-archive.md`.
