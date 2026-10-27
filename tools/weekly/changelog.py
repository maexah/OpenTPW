#!/usr/bin/env python3
"""changelog.py - the commits a weekly build's changelog is written from, listed before a word is written.

    changelog.py <new tag or rev> [--from <old tag or rev>]

Finds the newest weekly-YYYY-MM-DD tag that is an ancestor of <new> (dry-run weekly-test-N tags are passed over),
or takes --from, and prints: the range, how many commits, then each commit oldest first as
`[abc1234](commit link) subject`, with the areas its files touch (source/OpenTPW/World/Park -> World/Park, docs/exe ->
docs), and last the "full history" compare link between the two. The new tag need not exist yet: give `main` (or
HEAD) to list what a tag pushed there would cover, and the compare link names the tag the build will have
(weekly-<today>). With no earlier weekly tag and no --from, it stops and says so: the first build's notes are
written from a range chosen by hand. The release notes go in docs/releases/<tag>.md (docs/WORKFLOW.md, "Weekly builds")."""
import datetime, re, subprocess, sys

REPO_URL = "https://github.com/maexah/OpenTPW"


def git(*args):
    return subprocess.run(["git", *args], capture_output=True, text=True, check=True).stdout.strip()


def area(path):
    parts = path.split("/")
    if parts[0] == "source" and len(parts) > 2:
        project = parts[1]
        inner = [p for p in parts[2:-1]][:2]
        return "/".join(inner) if project == "OpenTPW" and inner else project
    if parts[0] == "docs":
        return "docs"
    return parts[0] if len(parts) > 1 else "(root)"


def main(a):
    if not a or a[0].startswith("-"):
        sys.exit(__doc__)
    new = a[0]
    old = a[a.index("--from") + 1] if "--from" in a else None
    head = git("rev-parse", new + "^{commit}")
    if old is None:
        tags = [t for t in git("tag", "--merged", head, "--list", "weekly-*", "--sort=-creatordate").splitlines()
                if re.fullmatch(r"weekly-\d{4}-\d{2}-\d{2}", t) and git("rev-parse", t + "^{commit}") != head]
        if not tags:
            sys.exit(f"no earlier weekly-YYYY-MM-DD tag below {new}: give --from <rev> for the first build's range")
        old = tags[0]
    shown = new if re.fullmatch(r"weekly-\d{4}-\d{2}-\d{2}", new) else f"weekly-{datetime.date.today():%Y-%m-%d}"
    log = git("log", "--reverse", "--no-merges", "--format=%H%x09%s", f"{old}..{head}").splitlines()
    print(f"Range {old}..{new} ({git('rev-parse', '--short=8', head)}): {len(log)} commits, merges left out\n")
    for line in log:
        sha, subject = line.split("\t", 1)
        files = git("diff-tree", "--no-commit-id", "--name-only", "-r", sha).splitlines()
        areas = sorted({area(f) for f in files})
        print(f"[{sha[:7]}]({REPO_URL}/commit/{sha}) {subject}")
        print(f"    areas: {', '.join(areas) or '(no files)'}")
    base = old if re.fullmatch(r"weekly-\d{4}-\d{2}-\d{2}", old) else git("rev-parse", "--short=8", old + "^{commit}")
    print(f"\nFull history: {REPO_URL}/compare/{base}...{shown}")


if __name__ == "__main__":
    main(sys.argv[1:])
