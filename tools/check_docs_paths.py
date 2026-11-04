#!/usr/bin/env python3
"""check_docs_paths.py - every path and type that README.md, CONTRIBUTING.md and ARCHITECTURE.md name still exists.

    check_docs_paths.py [file.md ...]     the three pages, or the files named

Looks at the text of code spans (each path-like word of a command, and of a fenced or indented block too) and at markdown
link targets and reference definitions (`[r]: a.md`; a line with more after the target than a title is a paragraph). It reads blocks as CommonMark does: a fenced block is three or more
backticks or tildes, closed by the same mark at least as long with nothing after it, indented up to three spaces past its
container (a list item's content column; four more is indented code); a quoted line (`> `) is read without its marker; a
heading is ATX (`#` to `######`, up to three spaces in) or setext (a paragraph over `===` or `---`). A code span is a run
of backticks closed by a run of the same length; a link inside one is not a link. A link target is `<...>` (spaces
allowed) or bare with balanced parentheses, then an optional title (`"..."`, `'...'` or `(...)`, with backslash escapes).
A dot-name with no slash (`.gitattributes`) is skipped: it cannot be told from a file type (`.wad`). A name that looks
like a path (it has a slash, or ends in a file type the repository holds) must match a tracked file or folder: whole, or
as the tail of a path (so `Client/GameDir.cs` is found under source/OpenTPW/). A name that looks like a type
(`ParkState`, `ParkState.CellAt`) must be declared as a class, struct, enum, interface or record in source/ (and a
member after the dot must appear there); a call (`ParkState.CellAt( x, y )`) is checked as its name. A link to another
page of the repository must reach a file, and its `#anchor` a heading outside a fence, by GitHub's slug of the heading as
rendered (`## _word_` is read as `word`, as CommonMark renders it; a repeated heading's is numbered -1, -2...). The game's own folders (`data/`, `save/`),
placeholders (`<kind>`, `*`) and web addresses are not checked. A code span with a bracket or parenthesis that does not
end in a file type (`(a+b)/50`, `open(tools/x.py)`) has its words checked as a command's; one holding `](` is a link
written as code and is not checked. Prints each name it could not find with the file and
line, and exits 1 if there is any. The maintainer's Saturday audit runs it (docs/WORKFLOW.md, "Hooks").
Python 3.8 or newer, nothing to install; run it from anywhere in the repository."""
import os, re, subprocess, sys, unicodedata

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(HERE)
PAGES = ["README.md", "CONTRIBUTING.md", "ARCHITECTURE.md"]

# Words in backticks that look like types or paths but are neither of ours.
NOT_OURS = {
    "OpenTPW", "OpenTPW.dll", "OpenTPW.exe", "MSTest", "Xvfb", "MoltenVK", "SDL2", "PowerShell", "NeoVeldrid", "Vulkan",
    "ModKit", "CLAUDE.local.md", "console.log", "Proton", "Escape", "Ctrl", "Alt", "Space", "Shift", "Enter", "Tab",
}
# Folders of the player's game, never in the repository.
GAME_FOLDERS = ("data/", "save/", "Data/", "Save/")
# Named on purpose though git ignores them, and the project's own GitHub names.
NOT_TRACKED_ON_PURPOSE = ("content/ReferenceScreenshots/", "maexah/")
# A fence's mark (three or more backticks or tildes) and what follows it on the line.
FENCE = re.compile(r"(`{3,}|~{3,})(.*)$")
QUOTE = re.compile(r" {0,3}> ?")
ATX = re.compile(r"(#{1,6})(?:[ \t]+(.*))?$")
SETEXT = re.compile(r"(=+|-+)[ \t]*$")
THEMATIC = re.compile(r"([-*_])[ \t]*(?:\1[ \t]*){2,}$")
LIST = re.compile(r"([-+*]|\d{1,9}[.)])(?=[ \t]|$)")
AUTOLINK = re.compile(r"<[A-Za-z][A-Za-z0-9+.-]{1,31}:[^\s<>]*>")
HTML_TAG = re.compile(r"</?[A-Za-z][A-Za-z0-9-]*(?:\s[^<>]*)?/?>|<!--.*?-->", re.S)
ASCII_PUNCT = set("!\"#$%&'()*+,-./:;<=>?@[\\]^_`{|}~")
TITLE_CLOSE = {'"': '"', "'": "'", "(": ")"}
PATH_TYPES = {"md", "py", "cs", "sh", "yml", "yaml", "csproj", "sln", "json", "txt", "shader", "props", "config", "toml"}


def tracked():
    out = subprocess.check_output(["git", "-C", REPO, "ls-files", "-z", "--cached", "--others", "--exclude-standard"]).decode("utf-8")
    return [p for p in out.split("\0") if p]


def slug(text):
    """GitHub's anchor for a heading's rendered text (github-slugger's `slug`): lower case, every character but a letter,
    digit, "_", "-" or space dropped, and each space a "-". Nothing is trimmed: a space a dropped tag leaves at an end
    is a "-"."""
    return re.sub(r"[^\w\- ]", "", text.lower()).replace(" ", "-")


def is_punct(ch):
    """CommonMark 0.29's (GitHub's cmark-gfm): an ASCII punctuation character or a Unicode P one, not a Unicode symbol."""
    return ch in ASCII_PUNCT or unicodedata.category(ch)[0] == "P"


def code_spans(line):
    """Each code span of a line as (start, end, content): a run of backticks closed by the next run of the same length
    (a backslash before a backtick outside a span makes it a plain one), with one space off each end of a padded span."""
    spans = []
    i, n = 0, len(line)
    while i < n:
        if line[i] == "\\":
            i += 2
            continue
        if line[i] != "`":
            i += 1
            continue
        j = i
        while j < n and line[j] == "`":
            j += 1
        run, k, close = j - i, j, -1
        while k < n:
            if line[k] != "`":
                k += 1
                continue
            e = k
            while e < n and line[e] == "`":
                e += 1
            if e - k == run:
                close = k
                break
            k = e
        if close < 0:
            i = j
            continue
        content = line[j:close]
        if content[0] == " " and content[-1] == " " and content.strip():
            content = content[1:-1]
        spans.append((i, close + run, content))
        i = close + run
    return spans


def definition(body):
    """The target of a link reference definition `[label]: target "title"` that starts body, or None where the line is
    not one (CommonMark 0.29, section 4.7): a label with a character other than a space and no unescaped bracket, the
    target (`<...>` or bare, not empty), and after it nothing, or a space and a title that closes the line or runs on to
    the next. A label with nothing after the colon takes its target from the next line, which is not read: ""."""
    m = re.match(r"\[((?:[^\[\]\\]|\\.)*)\]:[ \t]*", body)
    if not m or not m.group(1).strip() or body[1] == "^":
        return None
    rest = body[m.end():]
    if not rest.strip():
        return ""
    dest = read_destination(rest, 0)
    if dest is None or dest[1] == 0:
        return None
    target, i = dest
    after = rest[i:]
    if not after.strip():
        return target
    title = after.lstrip(" \t")
    if title == after or title[0] not in TITLE_CLOSE:
        return None
    close, j = TITLE_CLOSE[title[0]], 1
    while j < len(title) and title[j] != close:
        if title[j] == "\\":
            j += 1
        elif title[0] == "(" and title[j] == "(":
            return None
        j += 1
    return target if j >= len(title) or not title[j + 1:].strip() else None


def read_destination(s, i):
    """A link destination at s[i]: `<...>` (spaces allowed) or bare (no space, parentheses balanced); backslash escapes.
    Returns (target, index after it) or None."""
    n = len(s)
    out = []
    if i < n and s[i] == "<":
        j = i + 1
        while j < n and s[j] != ">":
            if s[j] == "<":
                return None
            if s[j] == "\\" and j + 1 < n and s[j + 1] in ASCII_PUNCT:
                j += 1
            out.append(s[j])
            j += 1
        return ("".join(out), j + 1) if j < n else None
    j, depth = i, 0
    while j < n:
        c = s[j]
        if c == "\\" and j + 1 < n and s[j + 1] in ASCII_PUNCT:
            out.append(s[j + 1])
            j += 2
            continue
        if c in " \t" or ord(c) < 32:
            break
        if c == "(":
            depth += 1
        elif c == ")":
            if depth == 0:
                break
            depth -= 1
        out.append(c)
        j += 1
    return ("".join(out), j) if depth == 0 else None


def read_link(s, i):
    """What follows `](` at s[i]: the destination, an optional title after a space, and the closing bracket. Returns
    (target, index after the bracket) or None."""
    n = len(s)
    while i < n and s[i] in " \t":
        i += 1
    dest = read_destination(s, i)
    if dest is None:
        return None
    target, i = dest
    k = i
    while k < n and s[k] in " \t":
        k += 1
    if k < n and s[k] == ")":
        return target, k + 1
    if k == i or k >= n or s[k] not in TITLE_CLOSE:
        return None
    close, j = TITLE_CLOSE[s[k]], k + 1
    while j < n and s[j] != close:
        if s[j] == "\\":
            j += 1
        elif s[k] == "(" and s[j] == "(":
            return None
        j += 1
    k = j + 1
    while k < n and s[k] in " \t":
        k += 1
    return (target, k + 1) if k < n and s[k] == ")" else None


def escaped(s, i):
    """Whether s[i] has an odd run of backslashes before it."""
    k = i
    while k > 0 and s[k - 1] == "\\":
        k -= 1
    return (i - k) % 2 == 1


def link_targets(line, masked):
    """The targets of the inline links `[text](target "title")` of a line; `masked` is the line with its code spans
    blanked, which no `](` is read from."""
    found, pos = [], 0
    while True:
        i = masked.find("](", pos)
        if i < 0:
            return found
        link = None if escaped(masked, i) else read_link(line, i + 2)
        if link is None:
            pos = i + 2
        else:
            found.append(link[0])
            pos = link[1]


def emphasis(s, protected, drop, lo, hi):
    """CommonMark's emphasis on the `*` and `_` of s[lo:hi] that are not protected: marks the delimiters that pair up (one
    from each side at a time: a strong pair is two matches, which drops the same characters)."""
    delims, k = [], lo
    while k < hi:
        if s[k] not in "*_" or protected[k]:
            k += 1
            continue
        j = k
        while j < hi and s[j] == s[k] and not protected[j]:
            j += 1
        before, after = (s[k - 1] if k else " "), (s[j] if j < len(s) else " ")
        left = not after.isspace() and (not is_punct(after) or before.isspace() or is_punct(before))
        right = not before.isspace() and (not is_punct(before) or after.isspace() or is_punct(after))
        if s[k] == "*":
            can_open, can_close = left, right
        else:
            can_open, can_close = left and (not right or is_punct(before)), right and (not left or is_punct(after))
        delims.append({"ch": s[k], "lo": k, "hi": j, "n": j - k, "open": can_open, "close": can_close})
        k = j
    for ci, closer in enumerate(delims):
        while closer["close"] and closer["hi"] > closer["lo"]:
            for opener in reversed(delims[:ci]):
                both = (opener["open"] and opener["close"]) or (closer["open"] and closer["close"])
                if (opener["ch"] == closer["ch"] and opener["open"] and opener["hi"] > opener["lo"]
                        and not (both and (opener["n"] + closer["n"]) % 3 == 0 and not (opener["n"] % 3 == 0 and closer["n"] % 3 == 0))):
                    break
            else:
                break
            drop[opener["hi"] - 1] = drop[closer["lo"]] = True
            opener["hi"] -= 1
            closer["lo"] += 1
            for between in delims[delims.index(opener) + 1:ci]:
                between["lo"] = between["hi"]


def plain(markup):
    """The text a heading's inline markup renders to (what GitHub slugs): code spans, inline HTML, `[text](target)`
    links and images (an image's `!` stays: the slug drops it), backslash escapes and emphasis resolved. A reference
    link (`[text][ref]`) and an entity are left as written."""
    n = len(markup)
    drop, protected, settled = [False] * n, [False] * n, [False] * n
    for start, end, content in code_spans(markup):
        ticks = 0
        while markup[start + ticks] == "`":
            ticks += 1
        for k in range(start, end):
            protected[k] = True
        for k in list(range(start, start + ticks)) + list(range(end - ticks, end)):
            drop[k] = True
        if len(content) != end - start - 2 * ticks:
            drop[start + ticks] = drop[end - ticks - 1] = True
    masked = "".join("\0" if protected[k] else c for k, c in enumerate(markup))
    pos, floor = 0, 0
    while True:
        i = masked.find("](", pos)
        if i < 0:
            break
        link = None if escaped(masked, i) else read_link(markup, i + 2)
        pos = i + 2
        if link is None:
            continue
        depth, j = 0, i - 1
        while j >= floor:
            if not protected[j] and not escaped(masked, j):
                if masked[j] == "]":
                    depth += 1
                elif masked[j] == "[":
                    if depth == 0:
                        break
                    depth -= 1
            j -= 1
        if j < floor:
            continue
        emphasis(markup, [p or d or t for p, d, t in zip(protected, drop, settled)], drop, j + 1, i)
        for k in range(j + 1, i):
            settled[k] = True
        drop[j] = drop[i] = True
        if not (j > 0 and markup[j - 1] == "!" and not escaped(markup, j - 1)):
            floor = j + 1
        for k in range(i + 1, link[1]):
            drop[k] = protected[k] = True
        pos = link[1]
    for pattern, whole in ((AUTOLINK, False), (HTML_TAG, True)):
        for m in pattern.finditer(markup):
            if any(protected[m.start():m.end()]) or escaped(markup, m.start()):
                continue
            for k in range(m.start(), m.end()):
                protected[k] = True
                drop[k] = whole
            drop[m.start()] = drop[m.end() - 1] = True
    k = 0
    while k < n:
        if markup[k] == "\\" and not protected[k] and k + 1 < n and markup[k + 1] in ASCII_PUNCT and not protected[k + 1]:
            drop[k] = True
            protected[k + 1] = True
            k += 1
        k += 1
    emphasis(markup, [p or d or t for p, d, t in zip(protected, drop, settled)], drop, 0, n)
    return "".join(c for k, c in enumerate(markup) if not drop[k])


def strip_quotes(line, limit=None):
    """A line without the `>` markers of its block quotes (up to `limit` of them): (how many, the rest)."""
    depth = 0
    while limit is None or depth < limit:
        m = QUOTE.match(line)
        if not m:
            break
        line = line[m.end():]
        depth += 1
    return depth, line


def starts_block(body):
    """Whether a line (past its indent) begins a block that ends an open paragraph."""
    if ATX.match(body) or THEMATIC.match(body) or body.startswith(">"):
        return True
    fence = FENCE.match(body)
    if fence and not (fence.group(1)[0] == "`" and "`" in fence.group(2)):
        return True
    item = LIST.match(body)
    return bool(item and body[item.end():].strip() and (not item.group(1)[0].isdigit() or item.group(1)[:-1] == "1"))


def blocks(lines):
    """Each line of a page as (number, text, kind), read as CommonMark's blocks: kind is "fence" for a fence's own line,
    "code" inside a fenced block or an indented one (the text of a quoted line is without its `>`), "def" for a link
    reference definition, else "text"; and after a heading's line (or a setext heading's underline) one more entry,
    kind "heading", whose text is the heading's markup. A heading is ATX or setext; one inside a quote or a list item is
    not given (whether GitHub numbers it, and how, is not measured). The state: the open fence (mark,
    quote depth, container column), the content columns of the open list items, the open paragraph. A page that opens
    with `---` has YAML front matter up to the next `---` or `...`, which GitHub shows as a table: plain "text"."""
    fence, items, para, para_depth, para_col, empty_item = None, [], [], 0, 0, False
    front = 0
    if lines and lines[0].rstrip() == "---":
        front = next((k + 1 for k in range(1, len(lines)) if lines[k].rstrip() in ("---", "...")), 0)
    for no, raw in enumerate(lines, 1):
        if no <= front:
            yield no, raw, "text"
            continue
        lead = re.match(r"[ \t]*", raw).group()
        raw = lead.expandtabs(4) + raw[len(lead):]
        if fence is not None:
            depth, rest = strip_quotes(raw, fence[1])
            indent = len(rest) - len(rest.lstrip(" "))
            if depth >= fence[1] and (not rest.strip() or indent >= fence[2]):
                m = FENCE.match(rest.strip()) if indent - fence[2] <= 3 else None
                if m and m.group(1)[0] == fence[0][0] and len(m.group(1)) >= len(fence[0]) and not m.group(2).strip():
                    fence = None
                    yield no, rest, "fence"
                else:
                    yield no, rest, "code"
                continue
            fence = None
        depth, rest = strip_quotes(raw)
        body = rest.lstrip(" ")
        indent = len(rest) - len(body)
        if not body.strip():
            if depth:
                while items and len(raw) - len(raw.lstrip(" ")) < items[-1]:
                    items.pop()
            if items and empty_item:
                items.pop()
            empty_item = False
            para = []
            yield no, rest, "text"
            continue
        empty_item = False
        lazy = bool(para) and (indent - (items[-1] if items else 0) >= 4 or not starts_block(body)) \
            and not (items and indent < items[-1] and LIST.match(body))
        shallow = lazy and (depth < para_depth or indent < para_col)
        if lazy and depth < para_depth:
            depth = para_depth
        if para and depth != para_depth:
            para = []
        if not lazy:
            popped = False
            while items and indent < items[-1]:
                items.pop()
                popped = True
            if popped:
                para = []
        shown = rest
        while True:
            top = depth == 0 and not items
            col = items[-1] if items else 0
            if indent - col >= 4 and not para:
                yield no, shown, "code"
                break
            if indent - col < 4:
                m = FENCE.match(body)
                if m and not (m.group(1)[0] == "`" and "`" in m.group(2)):
                    para = []
                    fence = (m.group(1), depth, col)
                    yield no, shown, "fence"
                    break
                m = ATX.match(body)
                if m:
                    para = []
                    yield no, shown, "text"
                    content = re.sub(r"(^|[ \t])#+[ \t]*$", "", (m.group(2) or "").strip()).strip()
                    if content and top:
                        yield no, content, "heading"
                    break
                m = SETEXT.match(body)
                if m and para and not shallow:
                    yield no, shown, "text"
                    if top:
                        yield no, "\n".join(line.rstrip() for line in para), "heading"
                    para = []
                    break
                if THEMATIC.match(body):
                    para = []
                    yield no, shown, "text"
                    break
                m = LIST.match(body)
                after = body[m.end():] if m else ""
                if m and not (para and (not after.strip() or (m.group(1)[0].isdigit() and m.group(1)[:-1] != "1"))):
                    spaces = len(after) - len(after.lstrip(" "))
                    gap = spaces if 1 <= spaces <= 4 and after.strip() else 1
                    items.append(indent + len(m.group(1)) + gap)
                    para = []
                    if not after.strip():
                        empty_item = True
                        yield no, shown, "text"
                        break
                    body = after[gap:].lstrip(" ")
                    indent = items[-1] + len(after) - gap - len(body)
                    continue
                if not para and definition(body) is not None:
                    yield no, shown, "def"
                    break
            if not para:
                para_depth, para_col = depth, col
            para.append(body)
            yield no, shown, "text"
            break


def heading_slugs(text):
    """The anchors of a page's headings in order, as GitHub numbers a repeated one: the second `same` is `same-1`."""
    seen, out = {}, []
    for _, content, kind in blocks(text.split("\n")):
        if kind != "heading":
            continue
        base = name = slug(plain(content))
        while name in seen:
            seen[base] += 1
            name = f"{base}-{seen[base]}"
        seen[name] = 0
        out.append(name)
    return out


def headings(path):
    """The set of a page's heading anchors; empty for a page that cannot be read."""
    try:
        return set(heading_slugs(open(os.path.join(REPO, path), encoding="utf-8").read()))
    except OSError:
        return set()


def file_type(name):
    """The type after the last dot of a path's file name; "" when the file name has no dot."""
    file = name.rsplit("/", 1)[-1]
    return file.rsplit(".", 1)[1] if "." in file else ""


class Index:
    def __init__(self):
        self.files = tracked()
        self.dirs = set()
        for p in self.files:
            parts = p.split("/")
            for i in range(1, len(parts)):
                self.dirs.add("/".join(parts[:i]))
        self.cs = "\n".join(open(os.path.join(REPO, p), encoding="utf-8", errors="replace").read()
                            for p in self.files if p.startswith("source/") and p.endswith((".cs", ".config", ".csproj")))

    def path_exists(self, name):
        name = name.rstrip("/")
        for known in self.files:
            if known == name or known.endswith("/" + name) or known.startswith(name + "/") or ("/" + name + "/") in ("/" + known):
                return True
        return name in self.dirs or any(d.endswith("/" + name) for d in self.dirs)

    def type_exists(self, dotted):
        """A declared type (and a member after the dot that appears in the source), or a single name the source uses."""
        head, _, member = dotted.partition(".")
        if not member:
            return re.search(r"\b" + re.escape(head) + r"\b", self.cs) is not None
        if not re.search(r"\b(class|struct|enum|interface|record)\s+" + re.escape(head) + r"\b", self.cs):
            return False
        return re.search(r"\b" + re.escape(member) + r"\b", self.cs) is not None


def words_in(page, no, text, index):
    """The path-like words (a slash and a known file type) of a code line or a backticked command; each must exist. A full
    stop ending a word is the sentence's."""
    problems = []
    for word in re.split(r"[\s\"'=,;()]+", text):
        word = word.strip("`:").rstrip(".")
        if (not word or "<" in word or "*" in word or "$" in word or "{" in word or word.startswith(("/", "~", "%", "-", "http", "C:"))
                or word in NOT_OURS or word.startswith(GAME_FOLDERS + NOT_TRACKED_ON_PURPOSE)
                or word.startswith(("./", "../")) or "/" not in word or "//" in word):
            continue
        if file_type(word) in PATH_TYPES or word.endswith("/"):
            if not index.path_exists(word):
                problems.append((page, no, word, "no such file or folder"))
    return problems


def check(page, index):
    problems = []
    for no, line, kind in blocks(open(os.path.join(REPO, page), encoding="utf-8").read().split("\n")):
        if kind in ("fence", "heading"):
            continue
        if kind == "code":
            problems += words_in(page, no, line, index)
            continue
        spans = code_spans(line)
        masked = line
        for start, end, _ in spans:
            masked = masked[:start] + "\0" * (end - start) + masked[end:]
        targets = link_targets(line, masked)
        if kind == "def":
            target = definition(line.lstrip())
            targets += [target] if target else []
        for target in targets:
            if target.startswith(("http://", "https://", "mailto:")):
                continue
            file_part, _, anchor = target.partition("#")
            base = os.path.normpath(os.path.join(os.path.dirname(page), file_part)).replace(os.sep, "/") if file_part else page
            if file_part and not (base in index.files or base in index.dirs or base.rstrip("/") in index.dirs):
                problems.append((page, no, target, "link to a file that is not in the repository"))
            elif anchor and base.endswith(".md") and anchor not in headings(base):
                problems.append((page, no, target, "link to a heading that is not there"))
        for _, _, content in spans:
            name = content.strip()
            call = re.fullmatch(r"([A-Z][A-Za-z0-9]+\.[A-Za-z0-9]+)\(.*\)", name)
            if call:
                name = call.group(1)
            if " " in name:
                problems += words_in(page, no, name, index)
                continue
            if name in NOT_OURS or any(c in name for c in "<*$=") or "](" in name:
                continue
            if any(c in name for c in "[]()") and file_type(name) not in PATH_TYPES:
                problems += words_in(page, no, name, index)
                continue
            if name.startswith(("/", "~", "%", "-", "http", "C:")) or name.startswith(GAME_FOLDERS + NOT_TRACKED_ON_PURPOSE):
                continue
            if name.startswith(".") and "/" not in name:
                continue
            if re.fullmatch(r"OpenTPW(\.[A-Za-z]+)+", name) and ("source/" + name) in index.dirs:
                continue
            if "/" in name or file_type(name) in PATH_TYPES:
                if not index.path_exists(name):
                    problems.append((page, no, name, "no such file or folder"))
            elif re.fullmatch(r"[A-Z][A-Za-z0-9]+(\.[A-Z][A-Za-z0-9]+)?", name) and re.search(r"[a-z]", name):
                if not index.type_exists(name):
                    problems.append((page, no, name, "no such type (or member)"))
    return problems


def main(argv):
    pages = argv or PAGES
    index = Index()
    problems = []
    for page in pages:
        problems += check(page, index)
    print(f"check_docs_paths: {len(pages)} page(s), {len(problems)} name(s) not found")
    for page, no, name, why in problems:
        print(f"  {page}:{no}: `{name}`: {why}")
    return 1 if problems else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
