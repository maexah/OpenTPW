#!/usr/bin/env python3
"""check_docs_paths.py - every path and type that README.md, CONTRIBUTING.md and ARCHITECTURE.md name still exists.

    check_docs_paths.py [file.md ...]     the three pages, or the files named

Looks at the text between backticks (each path-like word of a command, and of a fenced block too) and at markdown link
targets (a title after the target is allowed). A fenced block is CommonMark's: three or more backticks or tildes, closed
by the same mark at least as long with nothing after it; it is found at any indent (a list item's fence sits deeper). A
dot-name with no slash (`.gitattributes`) is skipped: it cannot be told from a file type (`.wad`). A name that looks
like a path (it has a slash, or ends in a file type the repository holds) must match a tracked file or folder: whole, or
as the tail of a path (so `Client/GameDir.cs` is found under source/OpenTPW/). A name that looks like a type
(`ParkState`, `ParkState.CellAt`) must be declared as a class, struct, enum, interface or record in source/ (and a
member after the dot must appear there); a call (`ParkState.CellAt( x, y )`) is checked as its name. A link to another
page of the repository must reach a file, and its `#anchor` a heading outside a fence, by GitHub's slug (a repeated
heading's is numbered -1, -2...). The game's own folders (`data/`, `save/`), placeholders (`<kind>`, `*`) and web
addresses are not checked. Prints each name it could not find with the file and line, and exits 1 if there is any. The
maintainer's Saturday audit runs it (docs/WORKFLOW.md, "Hooks"). Python 3.8 or newer, nothing to install; run it from
anywhere in the repository."""
import os, re, subprocess, sys

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
# A link's target, then an optional title ("t", 't' or (t)) and spaces before the closing bracket.
LINK = re.compile(r"\]\(\s*([^)\s]+)(?:\s+(?:\"[^\"]*\"|'[^']*'|\([^)]*\)))?\s*\)")
# A fence's mark (three or more backticks or tildes) and what follows it on the line.
FENCE = re.compile(r"(`{3,}|~{3,})(.*)$")
PATH_TYPES = {"md", "py", "cs", "sh", "yml", "yaml", "csproj", "sln", "json", "txt", "shader", "props", "config", "toml"}


def tracked():
    out = subprocess.check_output(["git", "-C", REPO, "ls-files", "-z", "--cached", "--others", "--exclude-standard"]).decode("utf-8")
    return [p for p in out.split("\0") if p]


def slug(heading):
    """GitHub's anchor for a heading: lower case, every character but a letter, digit, "_", "-" or space dropped, and
    each space a "-". Markup is not rendered first, so `_word_` keeps its underscores where GitHub's would not."""
    text = re.sub(r"[^\w\- ]", "", heading.strip().lower())
    return text.replace(" ", "-")


def fenced(lines):
    """Each line with its number and whether it is inside a fenced block: True inside, False outside, None for a
    fence's own line. A backtick fence's info string holds no backtick (```x``` is inline code)."""
    fence = None
    for no, line in enumerate(lines, 1):
        m = FENCE.match(line.strip())
        if fence is None and m and not (m.group(1)[0] == "`" and "`" in m.group(2)):
            fence = m.group(1)
            yield no, line, None
        elif fence is not None and m and m.group(1)[0] == fence[0] and len(m.group(1)) >= len(fence) and not m.group(2).strip():
            fence = None
            yield no, line, None
        else:
            yield no, line, fence is not None


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


def headings(path):
    """The anchors of a page's headings, as GitHub numbers a repeated one: the second `same` is `same-1`. A closing run
    of hashes after a space (`## Closed ##`) is not part of the heading."""
    try:
        text = open(os.path.join(REPO, path), encoding="utf-8").read()
    except OSError:
        return set()
    seen = {}
    for _, line, inside in fenced(text.split("\n")):
        m = re.match(r"#{1,6}\s+(.*)$", line) if inside is False else None
        if not m:
            continue
        base = name = slug(re.sub(r"(^|\s)#+\s*$", "", m.group(1)))
        while name in seen:
            seen[base] += 1
            name = f"{base}-{seen[base]}"
        seen[name] = 0
    return set(seen)


def words_in(page, no, text, index):
    """The path-like words (a slash and a known file type) of a fenced line or a backticked command; each must exist."""
    problems = []
    for word in re.split(r"[\s\"'=,;()]+", text):
        word = word.strip("`:")
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
    for no, line, inside in fenced(open(os.path.join(REPO, page), encoding="utf-8").read().split("\n")):
        if inside is None:
            continue
        if inside:
            problems += words_in(page, no, line, index)
            continue
        for m in LINK.finditer(line):
            target = m.group(1)
            if target.startswith(("http://", "https://", "mailto:")):
                continue
            file_part, _, anchor = target.partition("#")
            base = os.path.normpath(os.path.join(os.path.dirname(page), file_part)).replace(os.sep, "/") if file_part else page
            if file_part and not (base in index.files or base in index.dirs or base.rstrip("/") in index.dirs):
                problems.append((page, no, target, "link to a file that is not in the repository"))
            elif anchor and base.endswith(".md") and anchor not in headings(base):
                problems.append((page, no, target, "link to a heading that is not there"))
        for m in re.finditer(r"`([^`]+)`", line):
            name = m.group(1).strip()
            call = re.fullmatch(r"([A-Z][A-Za-z0-9]+\.[A-Za-z0-9]+)\(.*\)", name)
            if call:
                name = call.group(1)
            if " " in name:
                problems += words_in(page, no, name, index)
                continue
            if name in NOT_OURS or "<" in name or "*" in name or "$" in name or "=" in name:
                continue
            if name.startswith(("/", "~", "%", "-", "http", "git ", "dotnet ", "C:")) or name.startswith(GAME_FOLDERS + NOT_TRACKED_ON_PURPOSE):
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
