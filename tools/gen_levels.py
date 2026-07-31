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
    python tools/gen_levels.py plan        # regenerate ALL 40 levels + write the C#
    python tools/gen_levels.py plan1/plan2 # regenerate one chapter only (no write)

The shipping ramp lives in CHAPTER1 / CHAPTER2 below. Par is capped at 12 by
design: a level should be hard to think about, not long to play.

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


def gen_classic(rng, w, h, n_walls, n_ents, par_lo, par_hi, min_dirs, max_dead,
                budget, good_dead=None):
    """Stage 1: pick a random start, explore once, then take a BFS-reachable
    state at the target depth as the bed layout. par is then exactly that depth.

    `good_dead` is an early-out: as soon as a candidate is this forgiving we take
    it and stop burning the budget (we ask for exact pars now, so "best of many"
    only ever tie-breaks on forgiveness anyway)."""
    best = None
    deadline = time.time() + budget
    while time.time() < deadline:
        if best is not None and good_dead is not None and best[1]["dead"] <= good_dead:
            break
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


def gen_sticky(rng, w, h, n_walls, n_ents, par_lo, par_hi, min_dirs, max_dead,
               budget, good_dead=None):
    """Stage 2: beds change the physics, so they must be chosen up front."""
    best = None
    deadline = time.time() + budget
    while time.time() < deadline:
        if best is not None and good_dead is not None and best[1]["dead"] <= good_dead:
            break
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


# ---------------------------------------------------------------- the ramp
# DESIGN RULE (the one that matters): a level is allowed to be hard to *think*
# about, but never long to *play*. Par is capped at 12 swipes, because a player
# who needs 12 optimal moves will really take 20-25 with the exploring, undoing
# and rethinking that a puzzle is supposed to involve. Difficulty comes from
# board shape, blocks and the number of animals - never from making the
# solution longer.
#
# Animal count ramps in plateaus, not jumps: a new animal count always gets
# two or three levels at an easy par before the par starts climbing again, so
# the player learns "how three animals behave" before being asked to be clever
# with three animals. Chapter 2 restarts the whole ramp from two animals,
# because sticky beds make it a new game.
#
# Columns: (w, h, walls, animals, par, min_dirs, max_dead, good_dead, seconds)
#   par       - exact BFS-optimal swipe count (lo == hi; this IS the 3-star bar)
#   min_dirs  - distinct swipe directions the optimal line must use (anti-trivial)
#   max_dead  - hard cap on the share of states that can strand the player
#   good_dead - stop searching early once a candidate is at least this forgiving
#
# Boards stay at 5 animals max: the C# StateKey packs positions in base 64, and
# 6 animals on a 7x7 blows the solver's state budget (measured: >200k states).
CHAPTER1 = [
    # -- one animal: learn the slide (par 2-4) --
    (4, 4, 0, 1,  2, 1, 0.02, 0.00, 10),
    (4, 4, 1, 1,  3, 2, 0.02, 0.00, 10),
    (4, 4, 2, 1,  4, 2, 0.04, 0.00, 12),
    # -- two animals: learn that one swipe moves everyone (par 4-6) --
    (4, 4, 1, 2,  4, 2, 0.06, 0.02, 15),
    (4, 4, 2, 2,  5, 2, 0.08, 0.03, 18),
    (5, 5, 2, 2,  5, 2, 0.08, 0.03, 18),
    (5, 5, 3, 2,  6, 3, 0.10, 0.04, 20),
    # -- three animals: learn to use each other as walls (par 6-8) --
    (5, 5, 2, 3,  6, 2, 0.12, 0.05, 22),
    (5, 5, 3, 3,  7, 3, 0.14, 0.06, 25),
    (5, 5, 3, 3,  7, 3, 0.16, 0.07, 25),
    (5, 5, 4, 3,  8, 3, 0.18, 0.08, 28),
    (6, 6, 3, 3,  8, 3, 0.18, 0.08, 28),
    # -- four animals: crowded rooms (par 8-10) --
    (6, 6, 3, 4,  8, 3, 0.20, 0.10, 30),
    (6, 6, 4, 4,  9, 3, 0.22, 0.10, 32),
    (6, 6, 4, 4,  9, 3, 0.22, 0.10, 32),
    (6, 6, 5, 4, 10, 3, 0.25, 0.12, 35),
    (6, 6, 5, 4, 10, 4, 0.25, 0.12, 35),
    # -- five animals: the full zoo (par 10-12) --
    (6, 6, 4, 5, 10, 3, 0.28, 0.14, 40),
    (7, 7, 5, 5, 11, 4, 0.30, 0.15, 45),
    (7, 7, 5, 5, 12, 4, 0.30, 0.15, 50),
]

CHAPTER2 = [
    # Sticky beds are a new game, so the ramp starts over at two animals and a
    # 3-move par. The generator additionally proves every one of these is
    # unsolvable (or strictly longer) under chapter-1 rules.
    (4, 4, 0, 2,  3, 2, 0.04, 0.00, 15),
    (4, 4, 1, 2,  4, 2, 0.06, 0.02, 18),
    (4, 4, 2, 2,  4, 2, 0.08, 0.03, 20),
    (5, 5, 1, 2,  5, 2, 0.10, 0.04, 22),
    # -- three animals --
    (5, 5, 2, 3,  5, 2, 0.12, 0.05, 25),
    (5, 5, 2, 3,  6, 3, 0.14, 0.06, 25),
    (5, 5, 3, 3,  6, 3, 0.16, 0.07, 28),
    (5, 5, 3, 3,  7, 3, 0.18, 0.08, 30),
    (6, 6, 3, 3,  7, 3, 0.18, 0.08, 30),
    # -- four animals --
    (6, 6, 3, 4,  8, 3, 0.20, 0.10, 35),
    (6, 6, 4, 4,  8, 3, 0.22, 0.10, 35),
    (6, 6, 4, 4,  9, 3, 0.24, 0.12, 40),
    (6, 6, 4, 4,  9, 3, 0.24, 0.12, 40),
    (6, 6, 5, 4, 10, 3, 0.26, 0.13, 45),
    # -- five animals --
    (6, 6, 4, 5, 10, 3, 0.28, 0.14, 50),
    (7, 7, 4, 5, 10, 3, 0.30, 0.15, 50),
    (7, 7, 5, 5, 11, 4, 0.32, 0.16, 55),
    (7, 7, 5, 5, 11, 4, 0.32, 0.16, 55),
    (7, 7, 6, 5, 12, 4, 0.34, 0.17, 60),
    (7, 7, 6, 5, 12, 4, 0.34, 0.17, 60),
]

HINTS1 = [
    "Swipe and your animal slides all the way to the wall.",
    "A toy block stops the slide. Use it to park where you want.",
    "Two blocks make a pocket. Slide in from the open side.",
    "Two friends now - one swipe moves them both.",
    "Line them up first, then send them home together.",
    "More room. A wrong-way swipe often sets up the right one.",
    "Bump one into a block to hold it while you place the other.",
    "Three friends. Handle the trickiest one first.",
    "Use one animal as a wall for another.",
    "Corners hold an animal still. Park someone in one.",
    "Think one move ahead before you swipe.",
    "Bigger room, longer slides. Group them, then split them off.",
    "A full den. Peel them off one at a time.",
    "Plan the last move first, then work backwards.",
    "Every swipe counts now. Look before you slide.",
    "Break the line up before you try to place anyone.",
    "Find the friend with only one way home.",
    "Five friends. Sweep them together, then sort them out.",
    "A bigger room. Decide the whole plan before the first swipe.",
    "The whole zoo, one last time. Then something changes...",
]

HINTS2 = [
    "Sticky beds tonight! Touch your own bed and you're in.",
    "You don't have to stop on your bed - sliding across it is enough.",
    "Blocks and sticky beds. Pick your approach carefully.",
    "Choose who goes to bed first. It changes everything after.",
    "A sleeping friend never gets up again - so they make a handy wall.",
    "You can't slide past your own bed any more. Plan the approach.",
    "Tuck the far ones in first - they leave the room emptier.",
    "A sleeper in the middle splits the room in two.",
    "Line them up and one long swipe can put two to bed.",
    "Wrong one asleep? Undo - a sleeper never gets up.",
    "Order matters more than direction here.",
    "Build a wall of sleepers, then slide the last one along it.",
    "Look for the animal whose bed is already in its path.",
    "The awkward one usually needs a sleeper to stop against.",
    "Five friends, five beds. Find the one that has to go last.",
    "Crowded room. Clear the corner before it fills up.",
    "Use the edges to line everyone up before you tuck anyone in.",
    "Every sleeper you place is a new wall. Place them kindly.",
    "Almost the last night. Take it slow.",
    "The whole zoo, sticky beds and all. Sweet dreams.",
]

BANNER1 = """            // ============================ CHAPTER 1 ============================
            // Beds are just destinations - land on one and the next swipe drags you
            // right back off it. Twenty levels of that is what makes chapter 2 land.
            // Pars run 2 -> 12: short enough to hold in your head, and the animal
            // count climbs in plateaus (1, 1, 1, 2, 2, 2, 2, 3, 3, ...) so every new
            // friend gets a couple of gentle levels before the thinking gets harder.
"""

BANNER2 = """
            // ============================ CHAPTER 2 ============================
            // STICKY BEDS. Touch your own bed - even mid-slide - and you're asleep
            // for the night. Every level here is impossible (or strictly longer)
            // under chapter 1's rules, so the twist isn't decoration: it's the only
            // way through. The ramp restarts at two animals and a 3-move par,
            // because sticky beds make this a new game to learn.
"""


def generate_plan(seed, chapters=(1, 2)):
    """Generate the whole shipping ramp and rewrite the Levels array in
    PuzzleGame.cs. Every par is exact and BFS-proven; nothing is hand-guessed."""
    import json
    rng = random.Random(seed)
    out = {1: [], 2: []}
    for chap, recipes, hints in ((1, CHAPTER1, HINTS1), (2, CHAPTER2, HINTS2)):
        if chap not in chapters:
            continue
        for idx, (w, h, nw, ne, par, md, dead, good, budget) in enumerate(recipes):
            gen = gen_sticky if chap == 2 else gen_classic
            lv = None
            for attempt in range(6):
                lv = gen(rng, w, h, nw, ne, par, par, md, dead, budget, good)
                if lv:
                    break
                # never widen the par: relax the shape instead, so the ramp holds
                dead = min(0.55, dead + 0.05)
                good = dead
                md = max(2, md - 1) if attempt >= 2 else md
            if not lv:
                print(f"// FAILED ch{chap} slot {idx+1} "
                      f"({w}x{h}, {ne} animals, par {par})", flush=True)
                continue
            lv["hint"] = hints[idx]
            out[chap].append(lv)
            print(f'// ch{chap} level {idx+1:2d}: {w}x{h} walls={nw} animals={ne} '
                  f'par={lv["par"]} dead={lv["dead"]:.2f} '
                  f'classic={lv.get("classic", "-")} '
                  f'line={"".join(DIRNAME[d][0] for d in lv["path"])}', flush=True)
    with open(PLAN_JSON, "w", encoding="utf-8") as f:
        json.dump({str(k): v for k, v in out.items()}, f, default=list, indent=1)
    if all(len(out[c]) == 20 for c in (1, 2)):
        write_cs(out)
        print(f"\nwrote 40 levels into {CS_PATH}")
    else:
        print(f"\nincomplete ({len(out[1])} + {len(out[2])}) - "
              f"C# not touched, plan saved to {PLAN_JSON}")
    return out


def write_cs(out, path=None):
    """Splice a fresh Levels array into PuzzleGame.cs, banners and all."""
    path = path or CS_PATH
    src = open(path, encoding="utf-8").read()
    head, rest = src.split("        private static readonly Lv[] Levels =\n        {\n", 1)
    tail = rest.split("\n        };\n", 1)[1]
    body = BANNER1 + "\n".join(emit(lv, lv["hint"]) for lv in out[1])
    body += "\n" + BANNER2 + "\n".join(emit(lv, lv["hint"]) for lv in out[2])
    new = (head + "        private static readonly Lv[] Levels =\n        {\n"
           + body + "\n        };\n" + tail)
    open(path, "w", encoding="utf-8", newline="\n").write(new)


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
PLAN_JSON = "tools/levels_plan.json"
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
    elif cmd == "plan":
        generate_plan(seed)
    elif cmd == "plan1":
        generate_plan(seed, chapters=(1,))
    elif cmd == "plan2":
        generate_plan(seed, chapters=(2,))
    else:
        print(__doc__)
