# docs/

Where the project's memory lives. Code says what; these files say why, and what was found.

| File | What goes in it | Who updates it |
|---|---|---|
| `STATUS.md` | What works, what does not, what was never verified. Short. Replaces the README's narrative. | Every task, last commit |
| `GLOSSARY.md` | One line per term: Peep, Thing, Object, Item, ParkWorld vs ParkState, Time vs GameClock... | Whenever a term is coined |
| `ARCHITECTURE.md` | How a frame runs, how a scene is built, who owns the tick, init order | When the shape changes |
| `DECISIONS.md` | Choices that were made and could have gone another way, one paragraph each, dated | When a choice is made |
| `WORKFLOW.md` | Branches, sessions, commits, how to verify | Rarely |
| `exe/` | Reverse-engineering facts as tables: addresses, offsets, field names, counts from shipped data | Whenever a fact is found |

## `exe/` pages

One page per subsystem of the original executable. Each page is a table with these columns:

`Address / offset` · `Original name (if known)` · `What it is` · `Where OpenTPW uses it` · `Evidence` · `Date`

Suggested pages (seed from the existing code comments and commit messages):

- `exe/addresses.md` – every `0x00...` function or global mentioned anywhere, one row each
- `exe/park-save.md` – the `.TPWS` / `Easymode.TPWI` layout: modules, cell layout, thing records, guest and staff fields (`+0x1f1 mQueuePos`, `+529 ...`)
- `exe/ride-script.md` – the `.RSE` container, the opcode table, which opcodes are implemented, and the per-opcode notes (`TRIGANIM` vs `TRIGANIM_CH`, `0x552fe5`)
- `exe/animation.md` – the `.MD2` animation half (the 190-line comment now at the top of `AnimationFile.cs`)
- `exe/model.md` – the mesh half of `.MD2`
- `exe/game-clock.md` – tick, pause, calendar (`0x785970`, `0x00402d90`, `31 ms`)
- `exe/item-description.md` – item flags, `NumSimultAnims`, queue cells
- `exe/audio.md` – `.SDT`, `cat_*.map`, Layer I vs II
- `exe/ui-layouts.md` – the compiled layout data the gadget and front end read

Rule: a fact is written here **once**. Code comments and commit messages point at the row; they do not repeat it.
