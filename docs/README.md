# docs/

Where the project's memory lives. Code says what; these files say why, and what was found.

| File | What goes in it | Who updates it |
|---|---|---|
| `STATUS.md` | What works, what does not, what was never verified. Short. Replaces the README's narrative. | Every task, last commit |
| `GLOSSARY.md` | One line per term: Peep, Thing, Object, Item, ParkWorld vs ParkState, Time vs GameClock vs the thing tick... | Whenever a term is coined |
| `VERIFYING.md` | **The numbered instrument rules.** Every one is a postmortem of a measurement that agreed with its author while being wrong. Other pages cite them by number, so a number never changes meaning. | Whenever an instrument lies |
| `QUEUE.md` | **The work queue.** Numbered items, bugs first, one per session, taken from the top unless Alexah reorders. Each is ticked in the commit that lands it. | Every task |
| `PLAYER-GAPS.md` | Gaps ordered by when a player meets each one. Not the queue: `QUEUE.md` returns to its open gaps 4, 5 and 7 after its own items (its section F). | As an item lands |
| `REVIEW-2026-09-21.md`, `REVIEW-2026-09-22.md` | The two dated codebase reviews `QUEUE.md` was drawn from; the detail behind Q1-Q12. Their line numbers are from the tip each was written against. | Never |
| `CLEANUP-PLAN.md` | Nine things a player saw, **all closed** 2026-09-22. **Untracked by instruction** — never staged, and deliberately not in `.gitignore`, so it shows in every `git status`. It exists on one machine and is absent from a fresh clone. `QUEUE.md` Q13 moves it into `history/`. | Never (closed) |
| `DECISIONS.md` | Choices that could have gone another way, one paragraph each, dated: what was chosen, why, and where its facts now live. | When a choice is made |
| `MEMORY-DIET.md` | What moved out of the memory folder and where, and what is still owed. | While the diet is unfinished |
| `WORKFLOW.md` | Branches, pushing, subagents (model by stage), sessions, commits, how to verify | Rarely |
| `exe/` | Reverse-engineering facts as tables: addresses, offsets, field names, counts from shipped data | Whenever a fact is found |
| FileFormats docs clone | File-format facts: the bytes in a shipped file. A separate repo, `OpenTPW.FileFormats` - see "The FileFormats docs clone" below. | Whenever a format fact is found |
| `history/` | Superseded plans and per-branch ledgers, **verbatim**. Grep it; never read it whole, and never edit it — history is kept as it was written. | Never |

**Two planned files do not exist yet:** **`ARCHITECTURE.md`** (how a frame runs, how a scene is built,
who owns the tick, init order) and **`TOOLING.md`** (the tooling recipes). `MEMORY-DIET.md` lists what is
owed to each. Do not go looking for them.

## The FileFormats docs clone

**What belongs there:** does it describe bytes in a file the game ships? Then it belongs in the FileFormats docs.
Does it describe what the executable does with them? Then it belongs in `exe/`. Mirror a format into its page in
the same session it is decoded or corrected (`CLAUDE.md` rule 14). Assert only what was verified against the real
game data and mark the rest as an open question; findings that are content rather than layout still belong there if
an implementer would trip over them.

**The pages:** an Astro/Starlight site, markdown under `src/content/docs/` (`formats/*.md`, `vm/*.md`). The sidebar
autogenerates, so a new page needs only `title: <Name> (*.ext)` frontmatter. Match the existing style - plain GFM
tables, `>` warnings, links written `](/formats/texture/)` - and run `npm run build` before every commit. A new
markdown syntax is not proven by a clean build: check the built HTML for it. The clone's path and the node
toolchain are in `CLAUDE.local.md`.

**Branches, and why they differ from this repo's.** The clone has `master`, which stays the mirror of
`upstream/master`; it feeds pull requests upstream, so `main`-is-tip does not apply to it. The `docs/*` branches
are disjoint stacks off `master`, so a page's content is a branch artifact: find the branch that owns a page with
`git log --oneline master..<branch> -- <path>` for every branch before editing it (`VERIFYING.md` rule 40), and
check every link against the headings on the branch you commit to. New pages go on a fresh branch off
`upstream/master`. When a page exists only on a pull request's branch, write on a new local branch stacked on it and
say so when asking about the push; local writes are always fine.

**Pull requests and pushes.** Nothing is pushed or opened without Alexah's yes (`CLAUDE.md` rule 1). Never push onto
a branch that carries an open pull request. Check that from here with `git ls-remote upstream 'refs/pull/*'`, which
lists each as `refs/pull/N/head`, and name the repository whenever you record a PR fact. Before any push: no
`refs/pull/*` on `origin`, the upstream PR heads unchanged, and each origin tip the expected parent; then one named
refspec per branch, no force.

**The published site is not serving** (it answers 403, and `docs.opentpw.org` is a parking page). Do not rewrite the
README's documentation links to guessed destinations; report them.

## `exe/` pages

One page per subsystem of the original executable. Where a subsystem page's table rows are addresses, they
use these columns (`addresses.md`, the index, has its own three: Address · What it is · Cited in):

`Address / offset` · `Original name (if known)` · `What it is` · `Evidence`

**Four columns, not six.** *Where OpenTPW uses it* is not one, because a `file:line` citation into our
own source rots within days — the fact lives here and the code points at it, never the other way round.
*Date* is not one either, because a dated fact invites a reader to weigh its age instead of its evidence.

| Page | What it covers |
|---|---|
| `exe/addresses.md` | Every address our source cites in `0x` form, one row each, with every file that cites it. Generated mechanically; an index, not the decode — the pages below hold that. |
| `exe/park.md` | What a park is made of on disk; the grid, item animation, the `.RSE` interpreter end to end, arrivals, and the save's map cells. Its format half is duplicated in the FileFormats clone and says so. |
| `exe/park-engine.md` | Park loading, the heightfield inside `base.MD2`, the state machine, camera and FOV, the save container, the sky, the clock and tick, key bindings, the gadget and camcorder, and the interaction modes: placing, the queue and path tools, selling, moving, hiring, the hand's ways out, leaving a park, and the per-object windows. |
| `exe/ride-operation.md` | A ride's turn on the thing sweep, the boarding chain, the state-to-handler maps, the queue and every way out of it, deciding and wandering, the staff turn, spending and a visit's effects, the object and guest fields, the WALK/BOUNCE/SCREAM families — and **where a peep is drawn**, both the rider carried on a ride's node and the walking peep interpolated per frame between two simulated positions. |
| `exe/hud.md` | The compiled layout-stream format, the mesh-name hash, every park panel's stream, the map's cell-to-pixel mapping. |
| `exe/weather.md` | Weather as thing model 15, "funny time" and its structural drift, the `.sam` schema compiled into the exe. |
| `exe/advisor-park.md` | The park advisor's eight scored message slots and its two tables. |
| `exe/ui.md` | The on-screen advisor and his interruption, the loading screen, the Escape menu and Game Options. |
| `exe/lobby.md` | Lobby layout streams, particles, the material flag word, the two gates, the lobby camera (attract, globe, the park-entry flight and its keys), island sound, the locale tables. |
| `exe/boot.md` | WinMain, boot init, and the main state machine's states. |
| `exe/scenes.md` | What the process shares between lobby and park, and the teardown order. |
| `exe/audio.md` | QMixer, the single sound entry point, how an effect plays (priority, voice classes, held chains, the voice pool), and the `.md2` node flags that mark an emitter. |
| `exe/render-states.md` | The compiled render-state word, CULL_NONE, the two alpha references, the three sorts the original does. |
| `exe/saves.md` | `Config.tcf`, `gms.dat`, and the `.TPWS` container preamble. |

Rule: a fact is written here **once**. Code comments and commit messages point at the row; they do not
repeat it. A confidence marker — unknown, unverified, refuted — is part of the fact and travels with it.
