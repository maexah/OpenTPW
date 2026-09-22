# docs/

Where the project's memory lives. Code says what; these files say why, and what was found.

| File | What goes in it | Who updates it |
|---|---|---|
| `STATUS.md` | What works, what does not, what was never verified. Short. Replaces the README's narrative. | Every task, last commit |
| `GLOSSARY.md` | One line per term: Peep, Thing, Object, Item, ParkWorld vs ParkState, Time vs GameClock vs the thing tick... | Whenever a term is coined |
| `VERIFYING.md` | **The numbered instrument rules.** Every one is a postmortem of a measurement that agreed with its author while being wrong. Other pages cite them by number, so a number never changes meaning. | Whenever an instrument lies |
| `PLAYER-GAPS.md` | The **tracked** work queue, ordered by when a player meets each gap. Alexah sets which one is the goal. | As an item lands |
| `CLEANUP-PLAN.md` | A **second** queue: things a player *sees*. **Untracked by instruction** — never staged, and deliberately not in `.gitignore`, so it shows in every `git status`. It exists on one machine and is absent from a fresh clone. | As an item lands |
| `MEMORY-DIET.md` | What moved out of the memory folder and where, and what is still owed. | While the diet is unfinished |
| `WORKFLOW.md` | Branches, sessions, commits, how to verify | Rarely |
| `exe/` | Reverse-engineering facts as tables: addresses, offsets, field names, counts from shipped data | Whenever a fact is found |
| `history/` | Superseded plans and per-branch ledgers, **verbatim**. Grep it; never read it whole, and never edit it — history is kept as it was written. | Never |

**Two rows of this table used to name files that do not exist**, and they are listed here as intentions
rather than quietly left as broken promises: **`ARCHITECTURE.md`** (how a frame runs, how a scene is
built, who owns the tick, init order) and **`DECISIONS.md`** (choices that could have gone another way,
one paragraph each, dated). `MEMORY-DIET.md` still owes `DECISIONS.md` a paragraph for each of the three
graphics-migration notes, and owes a `TOOLING.md` — which does not exist either — the tooling recipes.
Do not go looking for any of the three.

## `exe/` pages

One page per subsystem of the original executable. Each page is a table with these columns:

`Address / offset` · `Original name (if known)` · `What it is` · `Evidence`

**Four columns, not six.** *Where OpenTPW uses it* was dropped because a `file:line` citation into our
own source rots within days — the fact lives here and the code points at it, never the other way round.
*Date* was dropped because a dated fact invites a reader to weigh its age instead of its evidence.

| Page | What it covers |
|---|---|
| `exe/addresses.md` | Every address cited in our source, one row each. An index, not the decode — the pages below hold that. |
| `exe/park.md` | What a park is made of on disk, and the `.RSE` interpreter end to end. Its format half is duplicated in the FileFormats clone and says so. |
| `exe/park-engine.md` | Park loading, the heightfield inside `base.MD2`, the state machine, camera and FOV, the clock and tick, key bindings. |
| `exe/ride-operation.md` | A ride's per-tick turn, the boarding chain, the state-to-handler maps, the object and guest fields, the WALK/BOUNCE/SCREAM families — and **where a peep is drawn**, both the rider carried on a ride's node and the walking peep interpolated per frame between two simulated positions. |
| `exe/hud.md` | The compiled layout-stream format, the mesh-name hash, every park panel's stream, the map's cell-to-pixel mapping. |
| `exe/weather.md` | Weather as thing model 15, "funny time" and its structural drift, the `.sam` schema compiled into the exe. |
| `exe/advisor-park.md` | The park advisor's eight scored message slots and its two tables. |
| `exe/ui.md` | The on-screen advisor and his interruption, the loading screen, the Escape menu and Game Options. |
| `exe/lobby.md` | Lobby layout streams, particles, the material flag word, the two gates, the locale tables. |
| `exe/boot.md` | WinMain, boot init, and the main state machine's states. |
| `exe/scenes.md` | What the process shares between lobby and park, and the teardown order. |
| `exe/audio.md` | QMixer, the single sound entry point, and the `.md2` node flags that mark an emitter. |
| `exe/render-states.md` | The compiled render-state word, CULL_NONE, the two alpha references, the three sorts the original does. |
| `exe/saves.md` | `Config.tcf`, `gms.dat`, and the `.TPWS` container preamble. |

Rule: a fact is written here **once**. Code comments and commit messages point at the row; they do not
repeat it. A confidence marker — unknown, unverified, refuted — is part of the fact and travels with it.
