# History

The archives that used to live in Claude Code's project memory, moved here verbatim so they are
versioned and survive a disk failure. **Nothing here is current** - except the sections at the end of this page, which
index artifacts and point at what is still open.

| File | What it is |
|---|---|
| `current-task-archive.md` | The live tracker's own history: superseded plans, dated findings, the 42-thing Lost Kingdom census (grep `THE PARK IS FULLY DECODED`), the guest-meter table (grep `FUN_004fe1e0`). |
| `project-history-archive.md` | Finished work from branches 20-36, the two audits, and the 2026-09-12 review. |
| `contribution-branch-layout.md` | The per-branch ledger: what each branch did, its gate result, and its push record. |
| `fileformats-docs-ledger.md` | The FileFormats docs clone's per-commit ledger to 2026-09-21, and its 2026-09-18 audit, moved from memory on 2026-09-24. The clone's `git log` is authoritative; the rules it carried are in `../README.md`, "The FileFormats docs clone". |

## How to read it

**Grep it. Never read one of these whole**, and never quote a status line from here as the state of
the project. Every claim is frozen at the date it was written, and much of it was already superseded
when it was moved. `current-task-archive.md` alone contains three different test counts, a retracted
"OpenTPW is not yet a game" verdict, and navigation instructions for a file shape that no longer
exists. For where things actually stand, read `../STATUS.md`.

The decoded facts these files once held have moved to `../exe/`, one page per subsystem, with their
confidence markers intact. If a fact appears both here and there, **`../exe/` is the current one.**

## Why the branch ledger was kept

`MEMORY-DIET.md` first planned to drop the ledger as the git log. That is true of the commit
messages, which git does hold — but four things live only here, and each is a trap that costs a
session:

- A throwaway worktree must go on real disk, not the session scratchpad. That scratchpad is a small
  tmpfs; when it fills, commands exit 0 with truncated output, so a build-and-test gate appears to
  pass while measuring nothing.
- `git mv` stages a rename using the file's **indexed** content, so a class rename can stay unstaged
  and the commit will not compile even though the working tree builds. A green working-tree build is
  not evidence that a commit compiles.
- `git add -p` is unavailable in this environment. When one file carries changes belonging to two
  commits, re-cut the commits so their file sets are **disjoint** rather than trying to split a file.
- A test count written in a commit message is a claim about that commit **standing alone**. A
  full-suite run cannot check it.

Those four are now also in `../WORKFLOW.md`, beside the gate they qualify. The rest of the ledger is
kept because deleting a record to save 155 KB, when the cost of being wrong is losing the only copy
of something, is a bad trade.

## The 2026-09-12 review

A 27-agent review of the whole codebase and the five-phase repair plan it produced (A-E, plus F, opportunistic). Its three pages are
Alexah's own artifacts, **as of 2026-09-12** - they still show phase A unbuilt and phase D not started:

- The findings, "The Engine Under the Lobby": https://claude.ai/code/artifact/96d78485-ad02-4774-b0dd-c481a347e3d5
- The plan, "Order of Repairs": https://claude.ai/code/artifact/bcc2595c-715d-4ae8-8732-5b32ca28e807
- All 126 findings: https://claude.ai/code/artifact/1a052a3a-307a-479f-969d-f197b5504096

Update a page with the Artifact tool's `url`, never by publishing a second copy. Their source and the raw
finding data are on Alexah's machine (the path is in `CLAUDE.local.md`). What is still open is in
`../QUEUE.md` Q70-Q75. Decided on the day and still standing: phase C labels dead code rather than deleting
it, and everything under `VM/` stays (`CLAUDE.md` rule 3); CI is refused (rule 2); the `BFSTReader` locale bug
is left while no language is asked for; `.gitignore` for `.claude/`, `.vscode/` and `.mcp.json`, and ModKit as
a whole, are Alexah's call; the remaining lows and the nullable warnings are taken only in passing.

## The 2026-09-12 lobby plan

Alexah's "Keep the plan report handy" plan for finishing the lobby, as of 2026-09-12 (it shows only its first
phase done; items 1-4, 6-8 and half of 5 have landed since). Both are Alexah's own artifacts:

- The plan, "Finishing the Lobby": https://claude.ai/code/artifact/48cf6fc2-0874-404c-b7bf-a05fe4b16470
- The audit it came from, "Lobby vs testme.exe": https://claude.ai/code/artifact/2877b10e-2953-45b6-9675-376ee2e537c0
  (its findings in full are in `project-history-archive.md`, grep `2877b10e`)

What is still open is `../QUEUE.md` section G and Q32.

## The ride-VM plan

The plan that decided `VM/` stays, as of 2026-09-15: https://claude.ai/code/artifact/38c58b2c-66a2-48b0-9fb9-be979930a38f
(Alexah's; the decision is `CLAUDE.md` rule 3 and its "Words" section).
