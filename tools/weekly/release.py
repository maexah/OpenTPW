#!/usr/bin/env python3
"""release.py - the weekly build's zip, and the guard that keeps the game's content out of it.

    release.py package <publish dir> <tag> <rid> <out dir>   the zip OpenTPW-<tag>-<rid>.zip, then guard it
    release.py guard <zip>...                                 every entry checked against the allow-list

The zip holds one folder, OpenTPW-<tag>/, with the published build, README.txt (from beside this script), LICENSE.md,
THIRD-PARTY-NOTICES.md and the font's licence (content/fonts/LICENSE.txt). The guard fails (exit 1) when an entry is
outside that folder, matches none of ALLOW, has a file type the game's disc uses (the extensions in
source/OpenTPW/Client/DiscData.txt, the disc's own file list) or the name of a file on the disc, or when a file on
REQUIRED is missing: a zip of only the notes, or without the font or the notices, is refused. It prints how many
entries each rule let in, so a new kind of file is seen, not waved through. It decides nothing about the code: it
only stops a zip that would ship the game's files or leave out what the game or its licences need
(docs/WORKFLOW.md, "Weekly builds"). Python 3.8 or newer, nothing to install."""
import os, re, stat, sys, zipfile

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
DISC_LIST = os.path.join(REPO, "source", "OpenTPW", "Client", "DiscData.txt")

# What a published build is made of, as paths inside the zip's one folder. Nothing else may be in it.
ALLOW = [
    ("the notes", r"README\.txt|LICENSE\.md|THIRD-PARTY-NOTICES\.md"),
    ("the program", r"OpenTPW(\.exe)?|createdump(\.exe)?"),
    ("libraries", r"[A-Za-z0-9_.+-]+\.(dll|so|dylib)"),
    ("debug symbols", r"[A-Za-z0-9_.-]+\.pdb"),
    ("runtime settings", r"OpenTPW\.(deps|runtimeconfig)\.json|OpenTPW\.dll\.config"),
    ("the Vulkan driver's manifest (macOS)", r"MoltenVK_icd\.json"),
    ("shaders", r"content/shaders/[A-Za-z0-9_/-]+\.(shader|vert|frag)"),
    ("the loading screen's font and its licence", r"content/fonts/(Roboto-Medium\.ttf|LICENSE\.txt)"),
]
# What a zip must hold: the guard refuses one without any of these. Each is matched whole, like ALLOW. The shaders are
# named one by one (a test checks that every file in content/shaders is here); the runtime's core and the natives are
# patterns because each system spells them its own way.
REQUIRED = [
    ("the program", r"OpenTPW(\.exe)?"),
    ("OpenTPW.dll", r"OpenTPW\.dll"),
    ("OpenTPW.Common.dll", r"OpenTPW\.Common\.dll"),
    ("OpenTPW.Files.dll", r"OpenTPW\.Files\.dll"),
    ("the dependency list", r"OpenTPW\.deps\.json"),
    ("the runtime settings", r"OpenTPW\.runtimeconfig\.json"),
    ("the .NET runtime's core", r"(lib)?coreclr\.(dll|so|dylib)"),
    ("the .NET runtime's host", r"(lib)?hostfxr\.(dll|so|dylib)"),
    ("the .NET runtime's host policy", r"(lib)?hostpolicy\.(dll|so|dylib)"),
    ("SDL", r"(lib)?SDL2(-2\.0)?\.(dll|so|dylib)"),
    ("shaderc", r"(lib)?shaderc_shared\.(dll|so|dylib)"),
    ("SPIRV-Cross", r"(lib)?spirv-cross\.(dll|so|dylib)"),
    *[(f"the shader {name}", re.escape(f"content/shaders/{name}")) for name in (
        "3d.shader", "blit.shader", "guests.shader", "includes/3d.vert", "particles.shader", "people.shader",
        "sky.shader", "test.shader", "ui.shader", "unlit.shader", "water.shader", "weather.shader")],
    ("the loading screen's font", re.escape("content/fonts/Roboto-Medium.ttf")),
    ("the font's licence", re.escape("content/fonts/LICENSE.txt")),
    ("README.txt", r"README\.txt"),
    ("LICENSE.md", r"LICENSE\.md"),
    ("the third-party notices", r"THIRD-PARTY-NOTICES\.md"),
]
# Entries of a file type the disc also has (.txt) which are ours: the notes and the font's licence.
TEXT_EXEMPT = {"README.txt", "content/fonts/LICENSE.txt"}
# Executables inside the zip: kept runnable on Linux and macOS.
RUNNABLE = {"OpenTPW", "createdump"}


def disc():
    """The disc's file names (lower case) and file types, from the list the game itself counts against."""
    names, types = set(), set()
    for line in open(DISC_LIST, encoding="utf-8"):
        line = line.strip()
        if not line or line.startswith("#"):
            continue
        base = line.rsplit("/", 1)[-1].lower()
        names.add(base)
        if "." in base:
            types.add(base.rsplit(".", 1)[1])
    return names, types


def check(entries, top):
    """The problems with a zip's entry names (none for a clean zip), and how many entries each rule let in."""
    names, types = disc()
    problems, counts = [], {label: 0 for label, _ in ALLOW}
    for entry in entries:
        if entry.endswith("/"):
            continue
        if not entry.startswith(top + "/"):
            problems.append(f"{entry}: outside {top}/")
            continue
        inside = entry[len(top) + 1:]
        base = inside.rsplit("/", 1)[-1].lower()
        kind = base.rsplit(".", 1)[1] if "." in base else ""
        if base in names:
            problems.append(f"{entry}: a file of that name is on the game's disc")
            continue
        if kind in types and inside not in TEXT_EXEMPT:
            problems.append(f"{entry}: .{kind} is one of the game's file types")
            continue
        label = next((label for label, pattern in ALLOW if re.fullmatch(pattern, inside)), None)
        if label is None:
            problems.append(f"{entry}: not on the allow-list")
            continue
        counts[label] += 1
    held = [e[len(top) + 1:] for e in entries if e.startswith(top + "/") and not e.endswith("/")]
    for label, pattern in REQUIRED:
        if not any(re.fullmatch(pattern, inside) for inside in held):
            problems.append(f"missing: {label}")
    return problems, counts


def guard(path):
    with zipfile.ZipFile(path) as z:
        entries = z.namelist()
    tops = {e.split("/", 1)[0] for e in entries}
    top = tops.pop() if len(tops) == 1 else ""
    problems, counts = check(entries, top or "<one folder>")
    print(f"{os.path.basename(path)}: {len(entries)} entries in {top or 'more than one folder'}/: " +
          ", ".join(f"{n} {label}" for label, n in counts.items()))
    for p in problems:
        print(f"  REFUSED {p}")
    return not problems and bool(top)


def package(publish, tag, rid, out):
    if not re.fullmatch(r"weekly-(\d{4}-\d{2}-\d{2}|test-\d+)", tag):
        sys.exit(f"{tag}: not a weekly tag (weekly-YYYY-MM-DD, or weekly-test-N for a dry run)")
    top = f"OpenTPW-{tag}"
    os.makedirs(out, exist_ok=True)
    path = os.path.join(out, f"OpenTPW-{tag}-{rid}.zip")
    files = []
    for root, _, names in os.walk(publish):
        for name in names:
            full = os.path.join(root, name)
            files.append((full, os.path.relpath(full, publish).replace(os.sep, "/")))
    files += [(os.path.join(HERE, "README.txt"), "README.txt"), (os.path.join(REPO, "LICENSE.md"), "LICENSE.md"),
              (os.path.join(REPO, "THIRD-PARTY-NOTICES.md"), "THIRD-PARTY-NOTICES.md"),
              (os.path.join(REPO, "content", "fonts", "LICENSE.txt"), "content/fonts/LICENSE.txt")]
    with zipfile.ZipFile(path, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as z:
        for full, inside in sorted(files, key=lambda f: f[1]):
            info = zipfile.ZipInfo.from_file(full, f"{top}/{inside}")
            info.create_system = 3   # Unix, so the modes below are kept by unzip, Archive Utility and file-roller
            mode = 0o755 if inside in RUNNABLE else 0o644
            info.external_attr = (stat.S_IFREG | mode) << 16
            info.compress_type = zipfile.ZIP_DEFLATED
            with open(full, "rb") as f:
                z.writestr(info, f.read())
    print(f"made {path} ({os.path.getsize(path) // (1 << 20)} MB, {len(files)} files)")
    return guard(path)


if __name__ == "__main__":
    a = sys.argv[1:]
    if len(a) == 5 and a[0] == "package":
        sys.exit(0 if package(*a[1:]) else 1)
    if len(a) >= 2 and a[0] == "guard":
        sys.exit(0 if all([guard(p) for p in a[1:]]) else 1)
    sys.exit(__doc__)
