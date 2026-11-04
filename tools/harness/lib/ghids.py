#!/usr/bin/env python3
"""ghids.py - GitHub's own heading ids of a page, against the ones tools/check_docs_paths.py works out.

    ghids.py fetch <owner/repo> <ref> <out-dir> <page.md>...   save each page as GitHub renders it and as raw text
    ghids.py compare <out-dir> [checker.py]                    per page: the ids GitHub gave that the checker lacks

GitHub's render API gives no ids (they are added later in its page pipeline), so the only measure is the page itself:
`fetch` saves github.com/<owner>/<repo>/blob/<ref>/<page> (the rendered HTML) and raw.githubusercontent.com's text of
it, both at the same ref, and `compare` reads each heading's `<hN ...>text</hN><a id="user-content-ID"` out of the
HTML. A page GitHub renders only in part (the big ones) yields a prefix of its headings: the ids it did give are
compared and the page is marked "partial". Prints, per page, the ids GitHub gave that the checker does not have (a
false 'link to a heading that is not there'), and with the checker's `heading_slugs( text )` (a list, in order) also
the ones it has that GitHub lacks. Needs network for `fetch` only; the files are public pages, nothing is sent."""
import importlib.util, os, re, sys, urllib.request

HEADING = re.compile(r'<h([1-6]) tabindex="-1" class="heading-element" dir="auto">(.*?)</h\1><a id="user-content-([^"]*)"', re.S)


def fetch(repo, ref, out, pages):
    os.makedirs(os.path.join(out, "raw"), exist_ok=True)
    for page in pages:
        name = page.replace("/", "_")
        for url, dest in ((f"https://github.com/{repo}/blob/{ref}/{page}", os.path.join(out, name + ".html")),
                          (f"https://raw.githubusercontent.com/{repo}/{ref}/{page}", os.path.join(out, "raw", name))):
            with urllib.request.urlopen(url, timeout=90) as r, open(dest, "wb") as f:
                f.write(r.read())


def github_ids(html):
    """(rendered heading html, id) in page order."""
    return [(m.group(2), m.group(3)) for m in HEADING.finditer(html)]


def load_checker(path):
    spec = importlib.util.spec_from_file_location("checker", path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def compare(out, checker):
    bad = 0
    for name in sorted(os.listdir(os.path.join(out, "raw"))):
        raw = os.path.join(out, "raw", name)
        theirs = github_ids(open(os.path.join(out, name + ".html"), encoding="utf-8", errors="replace").read())
        if hasattr(checker, "heading_slugs"):
            ours = checker.heading_slugs(open(raw, encoding="utf-8").read())
        else:
            ours = sorted(checker.headings(os.path.abspath(raw)))
        partial = hasattr(checker, "heading_slugs") and len(theirs) < len(ours)
        missing = [(h, i) for h, i in theirs if i not in set(ours)]
        extra = [i for i in ours[:len(theirs)] if i not in {i for _, i in theirs}] if hasattr(checker, "heading_slugs") else []
        inorder = not hasattr(checker, "heading_slugs") or [i for _, i in theirs] == ours[:len(theirs)]
        print(f"{name}: github {len(theirs)} ids, checker {len(ours)}{' (partial)' if partial else ''}"
              f"{'' if inorder else ', ORDER DIFFERS'}")
        for h, i in missing:
            print(f"    github has `{i}` (heading html {h!r}), checker lacks it")
        for i in extra:
            print(f"    checker has `{i}`, github lacks it")
        bad += len(missing) + len(extra) + (0 if inorder else 1)
    print(f"ghids: {bad} difference(s)")
    return 1 if bad else 0


if __name__ == "__main__":
    here = os.path.dirname(os.path.abspath(__file__))
    default = os.path.join(here, "..", "..", "check_docs_paths.py")
    if len(sys.argv) >= 6 and sys.argv[1] == "fetch":
        fetch(sys.argv[2], sys.argv[3], sys.argv[4], sys.argv[5:])
    elif len(sys.argv) in (3, 4) and sys.argv[1] == "compare":
        sys.exit(compare(sys.argv[2], load_checker(sys.argv[3] if len(sys.argv) == 4 else default)))
    else:
        sys.exit(__doc__)
