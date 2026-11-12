#!/usr/bin/env python3
"""gap_census.py - every Unimplemented.Report name in the source, and whether docs/QUEUE.md names it (Q243).

    gap_census.py              the names the queue does not hold, by file and line; exit 1 if there is any
    gap_census.py --all        every name, held or not, with the queue lines that hold it
    gap_census.py --queue F    read F in place of docs/QUEUE.md

A thing counted and not built must have a queue item, or it may never be come back to. This reads every `.cs` file
under source/ but the tests (source/OpenTPW.Tests/), finds each call of `Unimplemented.Report( ... )` outside a comment,
and reads the names its argument can take:
- a plain string (`"SIDESHOW_WINDOW"`), every one in the argument (a `?:` or `switch` gives several);
- an interpolated string whose words are a name with holes (`$"BUMPER_TEMPLATE_{x}"`): a pattern, `*` for each hole,
  held when the queue holds the fixed part with a name's characters where a hole is (`BUMPER_TEMPLATE_3`, or the
  pattern itself);
- an interpolated message (`$"{Name}: COAST op out of 1..8"`): its first word in capitals of three or more
  (`COAST`) is the name; a message with no such word (`$"guest state {s}"`) is DYNAMIC: its names are known only when
  the game runs, and the debug console's `unimplemented` lists them;
- no string at all (`Report( gap )`): INDIRECT. Its names are the name-shaped strings (capitals, digits and `_`,
  holding a `_`) of the same file that no direct call uses, listed under the site to be checked by hand.
A name is held when docs/QUEUE.md has it as a whole word, or when one item writes it short as its tail in a code span
(`_PLAIN`) and writes in full another name with the same stem (`PARK_FILE_SOUND_VOICE_ONE_SHOT`). A MESSAGE's word held
says only that the queue names the instruction, not that an item owns that message's case: the held line ends "(word)".
Each line also gives the Q-numbers cited within twenty lines above the call ("near", "(open)" for an open item) and a
"dead by CONTENT" or "dead by CODE" written there, to help find the item a name belongs to.
Python 3.8 or newer, nothing to install; run it from anywhere in the repository."""
import os, re, sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(HERE)
CALL = "Unimplemented.Report"
# A name as the reports write them: capitals, digits and underscores, starting with a capital.
NAME = re.compile(r"^[A-Z][A-Z0-9_]*(?:0x[0-9A-Fa-f]+[A-Z0-9_]*)*$")
# A name-shaped string for an INDIRECT site: it must hold an underscore, so a lone word in capitals ("OK") is not one.
INDIRECT_NAME = re.compile(r"^[A-Z][A-Z0-9]*(?:_(?:[A-Z0-9]+|0x[0-9A-Fa-f]+))+$")
# The first word in capitals of an interpolated message: three or more, so "A" and "OK" are not taken.
MESSAGE_WORD = re.compile(r"\b[A-Z][A-Z0-9_]{2,}\b")
# How many lines above a call (and two below) are read for the Q-numbers and "dead by" labels its comments cite.
NEAR = 20
# An open item's first line in the queue, and its number.
OPEN_ITEM = re.compile(r"^- \[ \] \*\*(Q\d+[a-z]?)\.")


def blank(piece):
    """The piece as spaces, its newlines kept, so every offset and line number after it still holds."""
    return re.sub(r"[^\n]", " ", piece)


def mask(text):
    """Two copies of the text, the same length: comments blanked, and comments and every string or character literal
    blanked (where a call is looked for, so a call written inside a string is not one)."""
    out, code, i, n = [], [], 0, len(text)
    while i < n:
        c = text[i]
        if text.startswith("//", i):
            j = text.find("\n", i)
            j = n if j < 0 else j
            out.append(" " * (j - i))
            code.append(" " * (j - i))
            i = j
        elif text.startswith("/*", i):
            j = text.find("*/", i + 2)
            j = n if j < 0 else j + 2
            out.append(blank(text[i:j]))
            code.append(blank(text[i:j]))
            i = j
        elif c == '"' or (c in "$@" and i + 1 < n and text[i + 1] in '"$@'):
            j = skip_string(text, i)
            out.append(text[i:j])
            code.append(blank(text[i:j]))
            i = j
        elif c == "'":
            j = i + 1
            while j < n and text[j] != "'":
                j += 2 if text[j] == "\\" else 1
            out.append(text[i:j + 1])
            code.append(blank(text[i:j + 1]))
            i = j + 1
        else:
            out.append(c)
            code.append(c)
            i += 1
    return "".join(out), "".join(code)


def skip_string(text, i):
    """The index just past the string literal that starts at i (plain, verbatim @"", interpolated $"" or $@"")."""
    j = i
    prefix = ""
    while text[j] in "$@":
        prefix += text[j]
        j += 1
    verbatim, interp = "@" in prefix, "$" in prefix
    j += 1  # the opening quote
    n = len(text)
    while j < n:
        c = text[j]
        if not verbatim and c == "\\":
            j += 2
        elif verbatim and text.startswith('""', j):
            j += 2
        elif c == '"':
            return j + 1
        elif interp and text.startswith("{{", j):
            j += 2
        elif interp and c == "{":
            j = skip_hole(text, j)
        else:
            j += 1
    return n


def skip_hole(text, i):
    """The index just past the interpolation hole that opens at i, nested strings and braces read through."""
    depth, j, n = 0, i, len(text)
    while j < n:
        c = text[j]
        if c == '"' or (c in "$@" and j + 1 < n and text[j + 1] in '"$@'):
            j = skip_string(text, j)
            continue
        if c == "{":
            depth += 1
        elif c == "}":
            depth -= 1
            if depth == 0:
                return j + 1
        j += 1
    return n


def literals(arg):
    """Each string literal in arg as (kind, value): "plain" with its text, or "interp" with every hole made `*`."""
    found, i, n = [], 0, len(arg)
    while i < n:
        c = arg[i]
        if c == '"' or (c in "$@" and i + 1 < n and arg[i + 1] in '"$@'):
            j = skip_string(arg, i)
            body = arg[i:j]
            start = body.index('"')
            prefix, inner = body[:start], body[start + 1:-1]
            if "$" in prefix:
                out, k = [], 0
                while k < len(inner):
                    if inner.startswith("{{", k):
                        out.append("{")
                        k += 2
                    elif inner[k] == "{":
                        k = skip_hole(inner, k)
                        out.append("*")
                    else:
                        out.append(inner[k])
                        k += 1
                found.append(("interp", "".join(out)))
            else:
                found.append(("plain", inner))
            i = j
        else:
            i += 1
    return found


def argument(text, open_paren):
    """The text between the parenthesis at open_paren and the one that closes it."""
    depth, j, n = 0, open_paren, len(text)
    while j < n:
        c = text[j]
        if c == '"' or (c in "$@" and j + 1 < n and text[j + 1] in '"$@'):
            j = skip_string(text, j)
            continue
        if c == "(":
            depth += 1
        elif c == ")":
            depth -= 1
            if depth == 0:
                return text[open_paren + 1:j]
        j += 1
    return text[open_paren + 1:]


def sites(path, rel):
    """Each report call in one file as a dict: file, line, kind (NAME, PATTERN, MESSAGE, DYNAMIC, INDIRECT), names, arg."""
    with open(path, encoding="utf-8", errors="replace") as f:
        raw = f.read()
    text, code = mask(raw)
    raw_lines = raw.split("\n")
    found = []
    for m in re.finditer(re.escape(CALL) + r"\s*\(", code):
        line = text.count("\n", 0, m.start()) + 1
        arg = argument(text, m.end() - 1)
        lits = literals(arg)
        names, kinds = [], set()
        for kind, value in lits:
            if kind == "plain" and NAME.match(value):
                names.append(value)
                kinds.add("NAME")
            elif kind == "interp" and NAME.match(value.replace("*", "X")):
                names.append(value)
                kinds.add("PATTERN")
            else:
                word = MESSAGE_WORD.search(value)
                if word:
                    names.append(word.group(0))
                    kinds.add("MESSAGE")
                else:
                    kinds.add("DYNAMIC")
        if not lits:
            kinds.add("INDIRECT")
        kind = "INDIRECT" if "INDIRECT" in kinds else "DYNAMIC" if "DYNAMIC" in kinds and not names else \
            "MESSAGE" if "MESSAGE" in kinds else "PATTERN" if "PATTERN" in kinds else "NAME"
        window = "\n".join(raw_lines[max(0, line - 1 - NEAR):line + 2])
        near = sorted(set(re.findall(r"\bQ\d+[a-z]?\b", window)), key=lambda q: (int(re.sub(r"\D", "", q)), q))
        dead = sorted(set(m.upper() for m in re.findall(r"[Dd]ead by (CONTENT|CODE|GAP|content|code)", window)))
        found.append({"file": rel, "line": line, "kind": kind, "names": names, "arg": " ".join(arg.split()), "text": text,
                      "near": near, "dead": dead})
    return found


def census(repo):
    """Every report site under source/ but the tests, in file then line order, INDIRECT ones given the file's other names."""
    result = []
    source = os.path.join(repo, "source")
    for root, dirs, files in os.walk(source):
        dirs[:] = sorted(d for d in dirs if d not in ("bin", "obj") and not (root == source and d == "OpenTPW.Tests"))
        for name in sorted(files):
            if not name.endswith(".cs"):
                continue
            path = os.path.join(root, name)
            rel = os.path.relpath(path, repo).replace(os.sep, "/")
            found = sites(path, rel)
            direct = {n for s in found for n in s["names"]}
            for s in found:
                if s["kind"] == "INDIRECT":
                    shaped = re.findall(r'"([A-Z][A-Za-z0-9_]*)"', s["text"])
                    s["names"] = sorted({v for v in shaped if INDIRECT_NAME.match(v) and v not in direct})
                del s["text"]
            result.extend(found)
    return result


def items(queue_lines):
    """Each item of the queue as (first line number, last line number): from its `- [ ]` or `- [x]` line to the line
    before the next item or heading."""
    starts = [i for i, line in enumerate(queue_lines) if re.match(r"^- \[[ x]\] ", line)]
    blocks = []
    for k, start in enumerate(starts):
        end = start
        limit = starts[k + 1] if k + 1 < len(starts) else len(queue_lines)
        while end + 1 < limit and not queue_lines[end + 1].startswith("#"):
            end += 1
        blocks.append((start + 1, end + 1))
    return blocks


def holders(name, queue_lines, blocks=None):
    """The queue's line numbers holding name as a whole word; a pattern's `*` stands for one or more name characters.
    An item may write a name short, as its last parts after an underscore in a code span (`_PLAIN`), once the same item writes in full
    another name that shares the rest (`PARK_FILE_SOUND_VOICE_ONE_SHOT`): that is held too."""
    if "*" in name:
        fixed = [re.escape(part) for part in name.split("*")]
        pattern = re.compile(r"(?<![A-Za-z0-9_])" + r"(?:[A-Z0-9_]+|\*)".join(fixed) + r"(?![A-Za-z0-9_])")
    else:
        pattern = re.compile(r"(?<![A-Za-z0-9_])" + re.escape(name) + r"(?![A-Za-z0-9_])")
    held = [number for number, line in enumerate(queue_lines, 1) if pattern.search(line)]
    if held or "*" in name:
        return held
    parts = name.split("_")
    for first, last in blocks if blocks is not None else items(queue_lines):
        text = "\n".join(queue_lines[first - 1:last])
        for cut in range(1, len(parts)):
            stem, tail = "_".join(parts[:cut]) + "_", "_" + "_".join(parts[cut:])
            short = re.compile(r"`" + re.escape(tail) + r"(?![A-Za-z0-9_])")
            if short.search(text) and \
                    re.search(r"(?<![A-Za-z0-9_])" + re.escape(stem) + r"[A-Z0-9]", text):
                held.extend(n for n in range(first, last + 1) if short.search(queue_lines[n - 1]))
                break
    return sorted(set(held))


def main(argv):
    show_all = "--all" in argv
    queue = os.path.join(REPO, "docs", "QUEUE.md")
    if "--queue" in argv:
        queue = argv[argv.index("--queue") + 1]
    with open(queue, encoding="utf-8") as f:
        queue_lines = f.read().split("\n")
    open_items = {m.group(1) for m in (OPEN_ITEM.match(line) for line in queue_lines) if m}
    blocks = items(queue_lines)
    missing_sites = set()
    missing_names = set()
    kinds = {}
    for s in census(REPO):
        kinds[s["kind"]] = kinds.get(s["kind"], 0) + 1
        where = f"{s['file']}:{s['line']}"
        if s["kind"] == "DYNAMIC":
            if show_all:
                print(f"{where}: DYNAMIC Report( {s['arg']} ): named only at run time")
            continue
        if s["kind"] == "INDIRECT" and not s["names"]:
            missing_sites.add(where)
            missing_names.add(where)
            print(f"{where}: INDIRECT Report( {s['arg']} ): no name-shaped string in the file; find its names by hand")
            continue
        for name in s["names"]:
            held = holders(name, queue_lines, blocks)
            if not held:
                missing_sites.add(where)
                missing_names.add(name)
            if show_all or not held:
                state = "held at QUEUE.md:" + ",".join(map(str, held)) + (" (word)" if s["kind"] == "MESSAGE" else "") if held else "not in the queue"
                if s["near"]:
                    state += "; near: " + " ".join(q + ("(open)" if q in open_items else "") for q in s["near"])
                if s["dead"]:
                    state += "; dead by " + "/".join(s["dead"])
                via = f" (INDIRECT via Report( {s['arg']} ))" if s["kind"] == "INDIRECT" else f" ({s['kind']})" if s["kind"] != "NAME" else ""
                print(f"{where}: {name}{via}: {state}")
    total = sum(kinds.values())
    summary = ", ".join(f"{k} {kinds[k]}" for k in sorted(kinds))
    print(f"{total} report site(s) ({summary}); {len(missing_names)} name(s) not in the queue, at {len(missing_sites)} site(s)")
    return 1 if missing_names else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
