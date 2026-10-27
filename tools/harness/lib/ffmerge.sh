#!/bin/bash
# ffmerge.sh <repo> <branch> [main-name]: fast-forward the main branch (default main; master for FileFormats) to
# <branch> and delete it. Checks out the main branch first, refuses a dirty tree or a branch main is not an ancestor
# of, and prints the result. (2026-10-22: a merge run with the branch itself checked out did nothing.)
set -e
R=$1; B=$2; M=${3:-main}
cd "$R"
[ -z "$(git status --porcelain --untracked-files=no)" ] || { echo "ffmerge: $R has uncommitted changes"; exit 2; }
git merge-base --is-ancestor "$M" "$B" || { echo "ffmerge: $M is not an ancestor of $B (rebase first)"; exit 3; }
git checkout -q "$M"
git merge -q --ff-only "$B"
git branch -d "$B" >/dev/null
echo "ffmerge: $M now $(git log --oneline -1)"
