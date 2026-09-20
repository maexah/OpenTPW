# History

Three files that used to live in Claude Code's project memory, moved here verbatim so they are
versioned and survive a disk failure. **Nothing here is current.**

| File | What it is |
|---|---|
| `current-task-archive.md` | The live tracker's own history: superseded plans, dated findings, the 42-thing Lost Kingdom census (grep `THE PARK IS FULLY DECODED`), the guest-meter table (grep `FUN_004fe1e0`). |
| `project-history-archive.md` | Finished work from branches 20-36, the two audits, and the 2026-09-12 review. |
| `contribution-branch-layout.md` | The per-branch ledger: what each branch did, its gate result, and its push record. |

## How to read it

**Grep it. Never read one of these whole**, and never quote a status line from here as the state of
the project. Every claim is frozen at the date it was written, and much of it was already superseded
when it was moved. `current-task-archive.md` alone contains three different test counts, a retracted
"OpenTPW is not yet a game" verdict, and navigation instructions for a file shape that no longer
exists. For where things actually stand, read `../STATUS.md`.

The decoded facts these files once held have moved to `../exe/`, one page per subsystem, with their
confidence markers intact. If a fact appears both here and there, **`../exe/` is the current one.**

## Why the branch ledger was kept

`MEMORY-DIET.md` says the ledger is the git log and can be dropped. That is true of the commit
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
