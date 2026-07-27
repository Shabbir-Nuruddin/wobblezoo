"""
Level generator / verifier for Wobble Zoo ("Bedtime Shuffle").

Mirrors PuzzleGame.SlideSim + SolveFrom exactly, for both stages:

  Stage 1 (classic)  - one swipe slides every animal until it hits a wall,
                       a block, or another animal. Beds are just targets.
  Stage 2 (sticky)   - the same swipe, but the moment an animal touches its
                       OWN bed it snuggles in, stops there, and never moves
                       again (it becomes a soft wall for everyone else).

Every emitted level's `par` is the true BFS-optimal move count, so 3 stars is
always achievable and never a designer guess.

Usage:
    python tools/gen_levels.py verifycs    # re-prove every par in PuzzleGame.cs
    python tools/gen_levels.py verify      # prove this sim matches the C# one
    python tools/gen_levels.py stage1      # generate chapter-1 levels
    python tools/gen_levels.py stage2      # generate chapter-2 levels

`verifycs` is the one to run after ANY edit to the Levels array.
"""

import random
import sys
import time
from collections import deque

DIRS = [(1, 0), (-1, 0), (0, 1), (0, -1)]
DIRNAME = {(1, 0): "Right", (-1, 0): "Left", (0, 1): "Up", (0, -1): "Down"}


# ---------------------------------------------------------------- simulation
def slide(pos, dirv, w, h, walls, beds=None):
    """One swipe. `beds` non-None => sticky-bed (stage 2) rules."""
    dx, dy = dirv
    n = len(pos)
    np_ = list(pos)
    order = sorted(range(n), key=lambda i: -(np_[i][0] * dx + np_[i][1] * dy))
    occ = set(np_)
    for i in order:
        if beds is not None and np_[i] == beds[i]:
            continue                       # already asleep: never moves again
        occ.discard(np_[i])
        p = np_[i]
        while True:
            q = (p[0] + dx, p[1] + dy)
            if q[0] < 0 or q[0] >= w or q[1] < 0 or q[1] >= h:
                break
            if q in walls or q in occ:
                break
            p = q
            if beds is not None and p == beds[i]:
                break                      # caught by its own bed mid-slide
        np_[i] = p
        occ.add(p)
    return tuple(np_)


def bfs(start, goal, w, h, walls, beds=None, cap=400000):
    """Shortest swipe count from start to goal. Returns (par, path) or (None, None)."""
    if start == goal:
        return 0, []
    came = {start: (None, None)}
    q = deque([start])
    while q:
        cur = q.popleft()
        for d in DIRS:
            ns = slide(cur, d, w, h, walls, beds)
            if ns in came:
                continue
            came[ns] = (cur, d)
            if ns == goal:
                path = []
                k = ns
                while came[k][0] is not None:
                    path.append(came[k][1])
                    k = came[k][0]
                path.reverse()
                return len(path), path
            q.append(ns)
        if len(came) > cap:
            return None, None
    return None, None


def explore(start, w, h, walls, beds=None, cap=120000):
    """Full reachable graph from start, in one pass.

    Returns (dist, came, rev) where `dist` is the true BFS optimal move count to
    every reachable state (so no second search is ever needed to find a par),
    `came` lets us replay the optimal swipe list, and `rev` is the reversed
    graph used to measure how forgiving a level is."""
    dist = {start: 0}
    came = {start: (None, None)}
    rev = {}
    q = deque([start])
    while q:
        cur = q.popleft()
        for d in DIRS:
            ns = slide(cur, d, w, h, walls, beds)
            rev.setdefault(ns, []).append(cur)
            if ns not in dist:
                dist[ns] = dist[cur] + 1
                came[ns] = (cur, d)
                q.append(ns)
        if len(dist) > cap:
            return None
    return dist, came, rev


def path_to(came, goal):
    path = []
    k = goal
    while came[k][0] is not None:
        path.append(came[k][1])
        k = came[k][0]
    path.reverse()
    return path


def dead_fraction(dist, rev, goal):
    """Share of reachable states from which the goal can no longer be reached.
    Low = a forgiving, cozy level; high = easy to paint yourself into a corner."""
    seen = {goal}
    q = deque([goal])
    while q:
        cur = q.popleft()
        for p in rev.get(cur, ()):
            if p not in seen:
                seen.add(p)
                q.append(p)
    return 1.0 - len(seen) / len(dist)


# ---------------------------------------------------------------- generation
def random_board(rng, w, h, n_walls, n_ents):
    cells = [(x, y) for x in range(w) for y in range(h)]
    rng.shuffle(cells)
    walls = set(cells[:n_walls])
    free = [c for c in cells[n_walls:]]
    rng.shuffle(free)
    start = tuple(free[:n_ents])
    return walls, start


def gen_classic(rng, w, h, n_walls, n_ents, par_lo, par_hi, min_dirs, max_dead, budget):
    """Stage 1: pick a random start, explore once, then take a BFS-reachable
    state at the target depth as the bed layout. par is then exactly that depth."""
    best = None
    deadline = time.time() + budget
    while time.time() < deadline:
        walls, start = random_board(rng, w, h, n_walls, n_ents)
        ex = explore(start, w, h, walls)
        if ex is None:
            continue
        dist, came, rev = ex
        cands = [s for s, d in dist.items() if par_lo <= d <= par_hi]
        rng.shuffle(cands)
        for goal in cands[:12]:
            if any(goal[i] == start[i] for i in range(n_ents)):
                continue
            path = path_to(came, goal)
            if len(set(path)) < min_dirs:
                continue
            dead = dead_fraction(dist, rev, goal)
            if dead > max_dead:
                continue
            score = (dist[goal], -dead)
            if best is None or score > best[0]:
                best = (score, dict(w=w, h=h, par=dist[goal], walls=sorted(walls),
                                    start=start, beds=goal, dead=dead, path=path))
    return best[1] if best else None


def gen_sticky(rng, w, h, n_walls, n_ents, par_lo, par_hi, min_dirs, max_dead, budget):
    """Stage 2: beds change the physics, so they must be chosen up front."""
    best = None
    deadline = time.time() + budget
    while time.time() < deadline:
        cells = [(x, y) for x in range(w) for y in range(h)]
        rng.shuffle(cells)
        walls = set(cells[:n_walls])
        free = cells[n_walls:]
        rng.shuffle(free)
        if len(free) < n_ents * 2:
            continue
        start = tuple(free[:n_ents])
        beds = tuple(free[n_ents:n_ents * 2])
        ex = explore(start, w, h, walls, beds)
        if ex is None:
            continue
        dist, came, rev = ex
        if beds not in dist:
            continue
        par = dist[beds]
        if not (par_lo <= par <= par_hi):
            continue
        path = path_to(came, beds)
        if len(set(path)) < min_dirs:
            continue
        dead = dead_fraction(dist, rev, beds)
        if dead > max_dead:
            continue
        # the twist must MATTER: the same board under stage-1 rules has to play
        # differently (longer, or outright impossible)
        cpar, _ = bfs(start, beds, w, h, walls, None, cap=120000)
        if cpar is not None and cpar <= par:
            continue
        score = (par, -dead)
        if best is None or score > best[0]:
            best = (score, dict(w=w, h=h, par=par, walls=sorted(walls),
                                start=start, beds=beds, dead=dead, path=path,
                                classic=cpar))
    return best[1] if best else None


# ---------------------------------------------------------------- C# emission
def emit(lv, hint):
    walls = ",".join(f"W2({x},{y})" for x, y in lv["walls"])
    walls = f"new[]{{ {walls} }}" if lv["walls"] else "new Vector2Int[0]"
    ents = ", ".join(f"new EntDef({s[0]},{s[1]}, {b[0]},{b[1]})"
                     for s, b in zip(lv["start"], lv["beds"]))
    return (f'            new Lv({lv["w"]},{lv["h"]},{lv["par"]},"{hint}",\n'
            f'                {walls},\n'
            f'                new[]{{ {ents} }}),')


# ---------------------------------------------------------------- shipped set
# The 16 levels already in PuzzleGame.cs, used to prove this simulator matches
# the C# one before anything new is trusted.
SHIPPED = [
    (4, 4, 2, [], [(2, 1, 3, 3)]),
    (4, 4, 3, [(0, 3)], [(0, 1, 1, 3)]),
    (4, 4, 4, [(1, 0), (2, 2)], [(3, 3, 3, 1)]),
    (4, 4, 5, [(2, 1)], [(3, 2, 0, 2), (0, 0, 0, 3)]),
    (4, 4, 6, [(0, 3), (1, 3)], [(2, 0, 3, 2), (0, 0, 3, 1)]),
    (5, 5, 5, [(1, 1), (1, 4)], [(4, 1, 3, 0), (0, 3, 2, 0)]),
    (5, 5, 7, [(2, 3), (3, 0), (4, 1)], [(2, 4, 4, 3), (0, 2, 4, 4)]),
    (5, 5, 9, [(1, 1), (3, 3), (4, 3)], [(2, 4, 2, 0), (2, 3, 4, 4)]),
    (5, 5, 8, [(2, 0), (4, 1)], [(3, 1, 3, 4), (2, 1, 1, 4), (3, 3, 4, 0)]),
    (5, 5, 10, [(1, 2), (2, 2), (4, 3)], [(0, 2, 4, 4), (2, 0, 3, 3), (3, 4, 4, 2)]),
    (6, 6, 10, [(3, 0), (3, 2), (5, 3)], [(0, 4, 4, 5), (0, 3, 5, 5), (5, 0, 5, 4)]),
    (6, 6, 11, [(1, 3), (2, 0), (3, 2), (4, 4)], [(3, 1, 5, 0), (0, 2, 5, 1), (3, 0, 4, 1)]),
    (6, 6, 14, [(1, 4), (3, 2), (4, 1), (4, 3)], [(5, 5, 0, 0), (3, 0, 1, 5), (2, 5, 0, 5)]),
    (6, 6, 14, [(1, 4), (2, 0), (4, 5)], [(1, 3, 4, 0), (2, 2, 3, 0), (0, 0, 0, 1), (0, 4, 1, 1)]),
    (6, 6, 14, [(1, 0), (2, 3), (4, 0), (4, 3)], [(4, 4, 5, 4), (1, 4, 5, 2), (3, 4, 5, 0), (4, 1, 5, 1)]),
    (6, 6, 17, [(1, 4), (2, 1), (3, 1), (4, 4), (5, 3)], [(4, 2, 5, 5), (3, 2, 4, 0), (2, 2, 5, 0), (0, 2, 5, 4)]),
]


def verify():
    ok = True
    for i, (w, h, par, walls, ents) in enumerate(SHIPPED):
        start = tuple((e[0], e[1]) for e in ents)
        beds = tuple((e[2], e[3]) for e in ents)
        got, path = bfs(start, beds, w, h, set(walls))
        flag = "OK " if got == par else "BAD"
        if got != par:
            ok = False
        print(f"{flag} level {i+1:2d}: shipped par={par} solver par={got}")
    print("simulator matches C#" if ok else "MISMATCH - do not trust generated levels")
    return ok


# ---------------------------------------------------------------- recipes
# Boards stay at 5 animals max: the C# StateKey packs positions in base 64, and
# 6 animals on a 7x7 blows the solver's state budget (measured: >200k states).
STAGE1_NEW = [
    # (w, h, walls, ents, par_lo, par_hi, min_dirs, max_dead, seconds)
    (6, 6, 4, 4, 14, 15, 3, 0.35, 45),
    (6, 6, 5, 5, 15, 16, 3, 0.35, 60),
    (7, 7, 5, 4, 16, 17, 4, 0.35, 60),
    (7, 7, 5, 5, 18, 19, 4, 0.40, 75),
]

STAGE2_NEW = [
    (4, 4, 0, 2, 2, 3, 2, 0.05, 15),
    (4, 4, 1, 2, 3, 4, 2, 0.10, 20),
    (4, 4, 2, 3, 3, 5, 2, 0.12, 25),
    (5, 5, 2, 2, 4, 6, 2, 0.12, 25),
    (5, 5, 2, 3, 5, 7, 3, 0.15, 30),
    (5, 5, 3, 3, 6, 8, 3, 0.18, 30),
    (5, 5, 3, 4, 7, 9, 3, 0.20, 35),
    (6, 6, 3, 3, 8, 10, 3, 0.22, 35),
    (6, 6, 4, 4, 8, 11, 3, 0.25, 40),
    (6, 6, 4, 4, 10, 13, 3, 0.25, 45),
    (6, 6, 5, 4, 11, 14, 3, 0.28, 45),
    (6, 6, 4, 5, 11, 14, 3, 0.30, 50),
    (6, 6, 5, 5, 12, 15, 4, 0.30, 55),
    (7, 7, 4, 4, 12, 15, 4, 0.30, 55),
    (7, 7, 5, 4, 13, 16, 4, 0.32, 55),
    (7, 7, 5, 5, 13, 17, 4, 0.32, 60),
    (7, 7, 5, 5, 15, 18, 4, 0.32, 70),
    (7, 7, 6, 5, 15, 19, 4, 0.35, 70),
    (7, 7, 6, 5, 16, 20, 4, 0.35, 75),
    (7, 7, 6, 5, 18, 24, 4, 0.40, 90),
]


def run(recipes, sticky, seed):
    rng = random.Random(seed)
    out = []
    for idx, (w, h, nw, ne, lo, hi, md, dead, budget) in enumerate(recipes):
        gen = gen_sticky if sticky else gen_classic
        lv = None
        for attempt in range(4):
            lv = gen(rng, w, h, nw, ne, lo, hi, md, dead, budget)
            if lv:
                break
            lo = max(2, lo - 1)
            hi += 2
            dead = min(0.55, dead + 0.06)
        if not lv:
            print(f"// FAILED to generate slot {idx+1} ({w}x{h}, {ne} animals)", flush=True)
            continue
        out.append(lv)
        print(f'// slot {idx+1}: {w}x{h} walls={nw} animals={ne} '
              f'par={lv["par"]} dead={lv["dead"]:.2f} '
              f'classic={lv.get("classic", "-")} '
              f'dirs={"".join(DIRNAME[d][0] for d in lv["path"])}', flush=True)
        print(emit(lv, "TODO hint"), flush=True)
    return out


CS_PATH = "Assets/Scripts/SleepyZoo/PuzzleGame.cs"
CHAPTER_SIZE = 20


def verify_cs(path=CS_PATH):
    """Parse the real Levels array out of PuzzleGame.cs and re-prove every par,
    using chapter 1 rules for levels 1-20 and sticky-bed rules for 21+."""
    import re
    src = open(path, encoding="utf-8").read()
    body = src.split("private static readonly Lv[] Levels =", 1)[1]
    body = body.split("\n        };", 1)[0]
    chunks = body.split("new Lv(")[1:]
    ok = True
    for i, chunk in enumerate(chunks):
        head = re.match(r"\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*,", chunk)
        w, h, par = (int(g) for g in head.groups())
        walls = {(int(a), int(b)) for a, b in re.findall(r"W2\((\d+),(\d+)\)", chunk)}
        ents = [tuple(int(g) for g in m) for m in
                re.findall(r"new EntDef\((\d+),(\d+),\s*(\d+),(\d+)\)", chunk)]
        start = tuple((e[0], e[1]) for e in ents)
        beds = tuple((e[2], e[3]) for e in ents)
        sticky = beds if i // CHAPTER_SIZE >= 1 else None
        rule = "sticky " if sticky else "classic"
        problems = []
        if len(set(start)) != len(start) or len(set(beds)) != len(beds):
            problems.append("duplicate cell")
        if walls & (set(start) | set(beds)):
            problems.append("entity on a wall")
        if any(s == b for s, b in zip(start, beds)):
            problems.append("starts on its own bed")
        got, _ = bfs(start, beds, w, h, walls, sticky)
        if got != par:
            problems.append(f"par is {got}, file says {par}")
        if problems:
            ok = False
        print(f'{"OK " if not problems else "BAD"} level {i+1:2d} '
              f'[{rule}] {w}x{h} {len(ents)} animals par={par}'
              + (("  <-- " + "; ".join(problems)) if problems else ""))
    print(f"\n{len(chunks)} levels checked - "
          + ("all pars are BFS-optimal" if ok else "PROBLEMS FOUND"))
    return ok


if __name__ == "__main__":
    cmd = sys.argv[1] if len(sys.argv) > 1 else "verify"
    seed = int(sys.argv[2]) if len(sys.argv) > 2 else 20260727
    if cmd == "verify":
        verify()
    elif cmd == "verifycs":
        sys.exit(0 if verify_cs() else 1)
    elif cmd == "stage1":
        run(STAGE1_NEW, False, seed)
    elif cmd == "stage2":
        run(STAGE2_NEW, True, seed)
