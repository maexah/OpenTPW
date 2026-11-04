#!/usr/bin/env python3
"""mdfuzz.py - random markdown against an independent CommonMark reader, for tools/check_docs_paths.py.

    mdfuzz.py inline [N] [seed]     N random heading texts: plain() against markdown-it's rendered text
    mdfuzz.py blocks [N] [seed]     N random pages: which lines are code, and which headings, against markdown-it's

Needs `markdown_it` (pip install markdown-it-py; the commonmark preset, so no tables or strikethrough). Prints the first
disagreements with the input that makes each, and a count; exits 1 if there are any. Left out of the comparison on purpose:
a `*` and a `!` (the slug drops them, whether or not a `*` paired or an image's `!` stayed), an entity (`&amp;`, not decoded
by the checker), a heading inside a quote or a list item (the checker gives none: GitHub's ids there are not measured), and a
page that opens with `---` (front matter is GitHub's, not CommonMark's). Known and kept: markdown-it reads the end of a link's
text as a space where CommonMark reads the `]` (`[__b._](u)` differs), and a few pages in 3000 differ in the nesting of a
quote, a list and a lazy line (Q1021). The checker is the one in tools/ beside this folder's parent, or the path in $CHECKER."""
import importlib.util, os, random, re, sys
from markdown_it import MarkdownIt

HERE = os.path.dirname(os.path.abspath(__file__))
spec = importlib.util.spec_from_file_location("checker", os.environ.get("CHECKER") or os.path.join(HERE, "..", "..", "check_docs_paths.py"))
checker = importlib.util.module_from_spec(spec)
spec.loader.exec_module(checker)
md = MarkdownIt("commonmark")

INLINE = ["a", "b", "word", "x_y", " ", " ", "_", "__", "*", "**", "`", "``", "[", "]", "(", ")", "\\", "<b>", "</b>", "!", ".", ",",
          "http://u_v", "<http://u_v>", "\"", "'", "<!-- c_d -->", "$", "%", "-", "(t)", "](u_v)", "](<u v>)", "](u 'ti_tle')", "€"]


def rendered(children):
    out = []
    for t in children:
        if t.type in ("text", "code_inline", "text_special"):
            out.append(t.content)
        elif t.type in ("softbreak", "hardbreak"):
            out.append("\n")
        elif t.type == "image":
            out.append(rendered(t.children or []))
        elif t.children:
            out.append(rendered(t.children))
        elif t.type == "image":
            out.append(t.content)
    return "".join(out)


def inline(n, rng):
    bad = 0
    for _ in range(n):
        text = "".join(rng.choice(INLINE) for _ in range(rng.randint(1, 9))).strip()
        if not text or "&" in text:
            continue
        tokens = md.parse("# " + text)
        heads = [t for t in tokens if t.type == "inline"]
        if len(tokens) != 3 or tokens[0].type != "heading_open" or not heads:
            continue
        want = rendered(heads[0].children)
        if "reference" in str(md.parse("# " + text)):
            continue
        got = checker.plain(text)
        if got.replace("*", "").replace("!", "").strip() != want.replace("*", "").replace("!", "").strip():
            bad += 1
            if bad <= 12:
                print(f"inline: {text!r}: checker {got!r}, markdown-it {want!r}")
    print(f"inline: {bad} disagreement(s)")
    return bad


BLOCK = ["", "", "text", "text _x_", "# H", "## H ##", "   ### H3", "    #### H4", "===", "---", "***", "- item", "1. item", "2. item",
         "  - nested", "   text more", "> quote", "> ```sh", "> > ```", "> ", "```sh", "```", "~~~", "````", "    code", "      code6", "  ```", "     ```",
         "   ```", "   # Indented", "Setext", "-", "[r]: a.md", "- ```sh", "1) ```", "* * *", "=== ", "---  ", "\t```", "\tcode"]


def blocks(n, rng):
    bad = 0
    for _ in range(n):
        lines = [rng.choice(BLOCK) for _ in range(rng.randint(2, 12))]
        while lines and not lines[-1].strip(" >"):
            lines.pop()
        if not lines or lines[0].rstrip() == "---":
            continue
        text = "\n".join(lines)
        want_code, want_heads = set(), []
        tokens = md.parse(text)
        for t in tokens:
            if t.type in ("fence", "code_block") and t.map:
                want_code.update(range(t.map[0] + 1, t.map[1] + 1))
            if t.type == "heading_open" and t.level == 0 and t.map and not any(x.type in ("blockquote_open", "list_item_open") for x in []):
                pass
        # headings only at level 0 (not in a quote or an item)
        want_heads = [tokens[i + 1].content for i, t in enumerate(tokens) if t.type == "heading_open" and t.level == 0]
        got_code, got_heads = set(), []
        for no, line, kind in checker.blocks(lines):
            if kind in ("code", "fence"):
                got_code.add(no)
            elif kind == "heading":
                got_heads.append(line)
        blank = {no for no, line in enumerate(lines, 1) if not line.strip(" >\t")}
        got_code, want_code = got_code - blank, want_code - blank
        flat = lambda hs: [re.sub(r"\s*\n\s*", "\n", h.strip()) for h in hs]
        if got_code != want_code or flat(got_heads) != flat(want_heads):
            bad += 1
            if bad <= 8:
                print(f"blocks: {lines!r}\n    code lines: checker {sorted(got_code)}, markdown-it {sorted(want_code)}\n    headings: checker {got_heads}, markdown-it {want_heads}")
    print(f"blocks: {bad} disagreement(s)")
    return bad


if __name__ == "__main__":
    if len(sys.argv) < 2 or sys.argv[1] not in ("inline", "blocks"):
        sys.exit(__doc__)
    n = int(sys.argv[2]) if len(sys.argv) > 2 else 2000
    rng = random.Random(int(sys.argv[3]) if len(sys.argv) > 3 else 1)
    sys.exit(1 if (inline if sys.argv[1] == "inline" else blocks)(n, rng) else 0)
