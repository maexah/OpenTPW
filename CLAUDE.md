# OpenTPW

@AGENTS.md

`AGENTS.md`, above, holds every rule and where things are; "`CLAUDE.md` rule N" in older pages means the same numbered rule
there. This file adds only what is particular to Claude Code. Your own machine's paths and working rules go in
`CLAUDE.local.md` (not committed; Claude Code loads it beside this file).

## Claude Code here

- **Subagent types** are in `.claude/agents/`: `tpw-gather` (greps and listings a script or the main session checks),
  `tpw-sweep` (first-pass read-only sweeps a verifier then checks) and `tpw-verify` (adversarial checks, Ghidra,
  decoding). Name the type on every subagent; `docs/WORKFLOW.md`, "Subagents", says which stage takes which model.
- **Ghidra** is reached through an MCP server if you have one set up (a `.mcp.json` of your own, not committed). Check
  it answers at the start of work (rule 7).
- **Worktrees**: an agent given `isolation: "worktree"` works under `.claude/worktrees/`; remove them and their
  `worktree-*` branches when done (rule 13).
