"""
Rebuild the whole Levels array in PuzzleGame.cs, in the right order.

Why this exists: `gen_levels.py plan` splices regenerated chapters into whatever
is already in the file, keeping the chapters it wasn't asked to touch. If a
chapter fails to generate, that splice silently leaves a HOLE — and because
chapters are addressed by index (ChapterStart), every chapter after the hole
shifts up and gets played under the wrong chapter's rules. That's a
level-breaking bug that looks fine in a diff.

So: this script builds the array from two sources of truth and refuses to write
anything unless the result is exactly the right length.

    chapters 1-2  from the last commit (they were verified, don't regenerate them)
    chapters 3-8  from tools/levels_plan.json (the generator's output)

    python tools/assemble_levels.py
"""

import json
import re
import subprocess
import sys

sys.path.insert(0, "tools")
import gen_levels as g   # noqa: E402


def committed_levels():
    """The Levels array text as it was at HEAD, split into one entry per level."""
    src = subprocess.run(["git", "show", f"HEAD:{g.CS_PATH}"],
                         capture_output=True, text=True, encoding="utf-8").stdout
    body = src.split("        private static readonly Lv[] Levels =\n        {\n", 1)[1]
    body = body.split("\n        };\n", 1)[0]
    chunks = body.split("            new Lv(")
    return ["            new Lv(" + c.rstrip() for c in chunks[1:]]


def main():
    plan = json.load(open(g.PLAN_JSON, encoding="utf-8"))
    old = committed_levels()
    print(f"committed levels available: {len(old)}")

    out = []
    for ch in range(1, len(g.CHAPTER_START) + 1):
        first, count = g.CHAPTER_START[ch - 1], g.CHAPTER_LEN[ch - 1]
        if str(ch) in plan:
            levels = plan[str(ch)]
            if len(levels) != count:
                sys.exit(f"chapter {ch} has {len(levels)} levels, needs {count}")
            out.append(f"\n            // ===================== CHAPTER {ch} ====================="
                       f"  ({g.TOY.get(ch, 'the basics')})")
            for k, lv in enumerate(levels):
                lv = dict(lv)
                lv["start"] = [tuple(s) for s in lv["start"]]
                lv["beds"] = [tuple(b) for b in lv["beds"]]
                for key in ("walls", "rugs", "honey", "holes"):
                    lv[key] = [tuple(c) for c in lv.get(key) or []]
                out.append(g.emit(lv, g.HINTS[ch][k]))
            print(f"chapter {ch}: {count} levels from the plan")
        else:
            if first + count > len(old):
                sys.exit(f"chapter {ch} is neither in the plan nor in the last commit")
            out.extend(old[first:first + count])
            print(f"chapter {ch}: {count} levels kept from the last commit")

    total = sum(g.CHAPTER_LEN)
    written = sum(1 for line in out if line.lstrip().startswith("new Lv("))
    if written != total:
        sys.exit(f"assembled {written} levels but the chapter table wants {total}")

    src = open(g.CS_PATH, encoding="utf-8").read()
    head, rest = src.split("        private static readonly Lv[] Levels =\n        {\n", 1)
    _, tail = rest.split("\n        };\n", 1)
    new = (head + "        private static readonly Lv[] Levels =\n        {\n"
           + "\n".join(out) + "\n        };\n" + tail)
    open(g.CS_PATH, "w", encoding="utf-8", newline="\n").write(new)
    print(f"\nwrote {written} levels to {g.CS_PATH}")


if __name__ == "__main__":
    main()
