"""
Level generator / verifier for Tuck In ("Bedtime Shuffle").

This is a second, independent implementation of the game's rules — it mirrors
PuzzleGame.SlideSim + SolveFrom exactly. Every level's `par` is the true
BFS-optimal move count, so a 3-star target is always achievable and never a
designer's guess.

THE RULES, per chapter (see `Rules` in PuzzleGame.cs — the two tables must agree):

    1 Bedtime Shuffle  one swipe slides every animal until something stops it
    2 Sleepyheads      + sticky beds: touch your own bed and you're asleep for good
    3 Musical Beds     + any animal may take any bed
    4 Slippery Rugs    + silk you can cross but never stop on
    5 Honey Puddles    + honey: touch it and you stop dead
    6 Rabbit Holes     + burrows in pairs: in one, out the other, keep sliding
    7 Heavy Sleepers   + one animal too heavy to slide unless it's pushed
    8 The Long Night   no new toy: rugs, honey and burrows together

Sticky beds stay on from chapter 2 onwards; everything after is a visible object
on the board, so each chapter adds one thing you can point at.

Usage:
    python tools/gen_levels.py audit         # re-prove every par in PuzzleGame.cs
    python tools/gen_levels.py plan          # regenerate ALL chapters + write the C#
    python tools/gen_levels.py plan 3 4 5    # regenerate only these chapters
    python tools/gen_levels.py selftest      # sanity-check the sim's own rules

`audit` (alias: `verifycs`) is the one to run after ANY edit to the Levels array.
It only proves the file agrees with THIS file, though — for the real check that
the C# and Python rules still agree, run the in-engine audit:

    Unity.exe -batchmode -quit -nographics -projectPath . -logFile audit.log \
              -executeMethod ChonkyMerge.EditorTools.LevelAudit.Run
"""

import random
import sys
import time
from collections import deque

DIRS = [(1, 0), (-1, 0), (0, 1), (0, -1)]
DIRNAME = {(1, 0): "Right", (-1, 0): "Left", (0, 1): "Up", (0, -1): "Down"}

CS_PATH = "Assets/Scripts/SleepyZoo/PuzzleGame.cs"
PLAN_JSON = "tools/levels_plan.json"

# Chapter layout must match ChapterStart in PuzzleGame.cs.
CHAPTER_START = [0, 20, 40, 55, 70, 85, 100, 115]
CHAPTER_LEN = [20, 20, 15, 15, 15, 15, 15, 15]


class Ctx:
    """Everything a swipe needs to know: the board, and this chapter's toys."""

    __slots__ = ("w", "h", "walls", "beds", "rugs", "honey", "holes",
                 "heavy", "sticky", "anybed", "bedset", "holemap")

    def __init__(self, w, h, walls, beds, rugs=(), honey=(), holes=(),
                 heavy=-1, sticky=False, anybed=False):
        self.w, self.h = w, h
        self.walls = frozenset(walls)
        self.beds = tuple(beds)
        self.rugs = frozenset(rugs)
        self.honey = frozenset(honey)
        self.holes = tuple(holes)
        self.heavy = heavy
        self.sticky = sticky
        self.anybed = anybed
        self.bedset = frozenset(beds)
        self.holemap = {}
        for k in range(0, len(self.holes) - 1, 2):
            a, b = self.holes[k], self.holes[k + 1]
            self.holemap[a] = b
            self.holemap[b] = a

    def inb(self, c):
        return 0 <= c[0] < self.w and 0 <= c[1] < self.h

    def is_bed_for(self, i, c):
        return c in self.bedset if self.anybed else c == self.beds[i]

    def asleep(self, pos, i):
        if not self.sticky:
            return False
        return pos[i] in self.bedset if self.anybed else pos[i] == self.beds[i]

    def goal(self, pos):
        if not self.anybed:
            return tuple(pos) == self.beds
        return self.bedset.issubset(set(pos))


# ---------------------------------------------------------------- simulation
def walk(ctx, i, frm, dirv, occ):
    """One animal's skid — the mirror of PuzzleGame.Walk."""
    dx, dy = dirv
    p = frm
    trail = [p]
    guard = ctx.w * ctx.h * 2 + 8
    while guard > 0:
        guard -= 1
        q = (p[0] + dx, p[1] + dy)
        if not ctx.inb(q) or q in ctx.walls or q in occ:
            break
        p = q
        exit_ = ctx.holemap.get(p)
        if exit_ is not None and exit_ not in occ and exit_ not in ctx.walls:
            p = exit_
            trail.append(p)
            if ctx.sticky and ctx.is_bed_for(i, p):
                break
            if p in ctx.honey:
                break
            continue
        trail.append(p)
        if ctx.sticky and ctx.is_bed_for(i, p):
            break
        if p in ctx.honey:
            break
    # silk: you may cross a rug but never come to rest on one
    while len(trail) > 1 and p in ctx.rugs:
        trail.pop()
        p = trail[-1]
    return p


def slide(ctx, pos, dirv):
    """One swipe — the mirror of PuzzleGame.SlideSim."""
    dx, dy = dirv
    np_ = list(pos)
    order = sorted(range(len(np_)), key=lambda i: -(np_[i][0] * dx + np_[i][1] * dy))
    occ = set(np_)
    for i in order:
        if ctx.asleep(np_, i):
            continue
        if i == ctx.heavy:
            continue                       # too heavy to move on its own
        occ.discard(np_[i])
        p = walk(ctx, i, np_[i], dirv, occ)
        guard = 0
        while ctx.heavy >= 0 and guard < 8:
            guard += 1
            ahead = (p[0] + dx, p[1] + dy)
            if not ctx.inb(ahead) or np_[ctx.heavy] != ahead or ctx.asleep(np_, ctx.heavy):
                break
            occ.discard(ahead)
            hp = walk(ctx, ctx.heavy, ahead, dirv, occ)
            occ.add(hp)
            np_[ctx.heavy] = hp
            if hp == ahead:
                break                      # it wouldn't budge, so neither can we
            p = walk(ctx, i, p, dirv, occ)
        np_[i] = p
        occ.add(p)
    return tuple(np_)


def explore(ctx, start, cap=200000):
    """Whole reachable graph from `start`, in one pass.

    Returns (dist, came, rev, goals): BFS depth to every reachable state, the
    parent links to replay an optimal line, the reversed graph (used to measure
    how forgiving a level is), and every state that counts as solved."""
    dist = {start: 0}
    came = {start: (None, None)}
    rev = {}
    goals = []
    if ctx.goal(start):
        goals.append(start)
    q = deque([start])
    while q:
        cur = q.popleft()
        for d in DIRS:
            ns = slide(ctx, cur, d)
            rev.setdefault(ns, []).append(cur)
            if ns not in dist:
                dist[ns] = dist[cur] + 1
                came[ns] = (cur, d)
                if ctx.goal(ns):
                    goals.append(ns)
                q.append(ns)
        if len(dist) > cap:
            return None
    return dist, came, rev, goals


def solve(ctx, start, cap=200000):
    """(par, path, goal_state) or (None, None, None)."""
    ex = explore(ctx, start, cap)
    if ex is None:
        return None, None, None
    dist, came, _, goals = ex
    if not goals:
        return None, None, None
    best = min(goals, key=lambda g: dist[g])
    return dist[best], path_to(came, best), best


def path_to(came, goal):
    path = []
    k = goal
    while came[k][0] is not None:
        path.append(came[k][1])
        k = came[k][0]
    path.reverse()
    return path


def dead_fraction(ctx, dist, rev, goals):
    """Share of reachable states from which NO solved state can still be reached.
    Low = a forgiving, cozy level; high = easy to paint yourself into a corner."""
    seen = set(goals)
    q = deque(goals)
    while q:
        cur = q.popleft()
        for p in rev.get(cur, ()):
            if p not in seen:
                seen.add(p)
                q.append(p)
    return 1.0 - len(seen) / len(dist)


# ---------------------------------------------------------------- the ramp
# DESIGN RULE: a level may be hard to *think* about but never long to *play*.
#
# This curve was rebuilt after play-testing on a phone. The old one reached FOUR
# animals and par 9 by level 15, and five animals at par 12 by level 20 — the
# hardest content in the game arrived inside the first twenty levels, in the
# chapter that is supposed to be teaching you to swipe. For a game people open in
# bed that isn't "challenging", it's work.
#
# Now: chapter one never goes past three animals or par 7, chapter two ends where
# chapter one used to *start* getting hard, and par is capped at 9 across the whole
# game instead of 12. Difficulty comes from board shape and the chapter's toy —
# never from making you hold a longer plan in your head.
CH1_ENTS  = [1, 1, 1, 2, 2, 2, 2, 2, 2, 2,  2, 2, 3, 3, 3, 3, 3, 3, 3, 3]
CH1_PAR   = [2, 2, 3, 3, 3, 3, 4, 4, 4, 4,  4, 4, 4, 4, 5, 4, 5, 4, 5, 5]
CH1_SIZE  = [4, 4, 4, 4, 4, 4, 5, 5, 5, 5,  5, 5, 5, 5, 5, 5, 5, 5, 5, 5]
# At least one block from level 2 onward. A bare 4x4 with one animal has almost no
# reachable states — every swipe just pins it to an edge — so an EXACT par is very
# often impossible and the generator grinds forever. One block is what makes a tiny
# board a puzzle at all.
CH1_WALLS = [0, 1, 1, 1, 1, 2, 1, 2, 2, 2,  2, 2, 1, 2, 2, 2, 3, 3, 3, 3]

CH2_ENTS  = [2, 2, 2, 2, 2, 3, 3, 3, 3, 3,  3, 3, 4, 4, 4, 4, 4, 4, 4, 4]
CH2_PAR   = [2, 3, 3, 3, 4, 3, 4, 4, 4, 4,  4, 5, 4, 4, 5, 5, 5, 5, 5, 5]
CH2_SIZE  = [4, 5, 5, 5, 5, 5, 5, 5, 5, 5,  6, 6, 6, 6, 6, 6, 6, 6, 6, 6]
CH2_WALLS = [0, 1, 1, 1, 2, 1, 2, 2, 2, 3,  2, 3, 2, 3, 3, 3, 3, 4, 4, 4]

# Chapters 3-8, fifteen slots each. Every chapter restarts at two animals, because
# every chapter hands the player a new toy to learn.
RAMP_ENTS  = [2, 2, 2, 3, 3, 3, 3, 3, 3, 4, 4, 4, 4, 4, 4]
RAMP_PAR   = [3, 3, 3, 3, 4, 4, 4, 4, 4, 4, 5, 5, 5, 5, 5]
RAMP_SIZE  = [5, 5, 5, 5, 5, 5, 6, 6, 6, 6, 6, 6, 6, 6, 6]
RAMP_WALLS = [0, 1, 1, 1, 2, 2, 2, 2, 3, 2, 3, 3, 3, 4, 4]
RAMP_TOYS  = [1, 1, 2, 2, 2, 2, 2, 2, 3, 3, 3, 3, 3, 3, 4]
RAMP_DEAD  = [.02, .02, .03, .03, .04, .04, .05, .05, .06, .06, .07, .07, .08, .08, .09]

# ---------------------------------------------------------------- the spikes
# The curve above is deliberately FLAT and short: par 2-5, forever. That is the
# whole point. This is a game people play in a car and in bed, and a puzzle that
# needs nine correct swipes in a row is not relaxing, it is admin.
#
# But a game with no resistance at all is wallpaper. So difficulty arrives as a
# small number of NAMED, PLACED spikes rather than as a rising tide:
#
#   MEDIUM, every ~30 levels — par 6. Still short, but the right first move is
#   genuinely not obvious. You will sit with it for a minute.
#
#   HARD, every ~40-45 levels — par 7 on a busier board, and the only levels in
#   the game where reaching for the hint is the expected outcome rather than a
#   failure.
#
# Seven spikes in 130 levels. Everything else is a gentle downhill on purpose:
# the reward loop lives in the dorm, not in beating the puzzle.
MEDIUM_LEVELS = {30, 60, 90, 120}      # 1-based level numbers
HARD_LEVELS   = {45, 85, 125}


def level_number(chapter, k):
    """1-based level number for slot k of a 1-based chapter."""
    return sum(CHAPTER_LEN[: chapter - 1]) + k + 1


# Chapter 7, Heavy Sleepers, gets its own table. Its big animal cannot move on its
# own, so one swipe per level is always spent pushing rather than placing — which
# means the shared ramp's pars all came out one higher than designed and produced
# five par-6 levels in a row at 111-115. The premium is baked in here instead, so
# the chapter reads like every other one from the player's side.
HEAVY_ENTS = [3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 4, 4, 4, 4]
HEAVY_PAR  = [4, 4, 4, 4, 4, 4, 5, 5, 5, 5, 5, 5, 5, 5, 5]

# chapter (1-based) -> which toy the generator scatters on the board.
# Chapter 0 isn't a chapter: it's the nightly-puzzle pool, which uses chapter one's
# rules and nothing else, so a daily puzzle can never lean on (or spoil) a twist the
# player hasn't reached. Chapters 1 and 2 have no scattered object at all — chapter
# one is the bare rule, chapter two adds sticky beds and nothing else.
TOY = {0: "none", 1: "none", 2: "sticky",
       3: "anybed", 4: "rugs", 5: "honey", 6: "holes", 7: "heavy", 8: "mixed"}

# The gentler chapters get their own tables; everything after shares the ramp.
CH_TABLES = {1: (CH1_SIZE, CH1_ENTS, CH1_PAR, CH1_WALLS),
             2: (CH2_SIZE, CH2_ENTS, CH2_PAR, CH2_WALLS)}


def spec_for(chapter, k):
    """One slot's recipe: chapter is 1-based, k is the slot within the chapter."""
    if chapter in CH_TABLES:
        sizes, ents_t, pars, walls_t = CH_TABLES[chapter]
        n, ents, par, walls = sizes[k], ents_t[k], pars[k], walls_t[k]
        # the teaching chapters stay forgiving: almost nothing can be wedged
        dead = min(0.14, 0.02 + k * 0.007)
        toys = 0
    else:
        n, ents, par = RAMP_SIZE[k], RAMP_ENTS[k], RAMP_PAR[k]
        walls, toys, dead = RAMP_WALLS[k], RAMP_TOYS[k], RAMP_DEAD[k]
        if TOY[chapter] == "heavy":
            ents, par = HEAVY_ENTS[k], HEAVY_PAR[k]
    # Heavy Sleepers spends one of its animals on the big one, which can't move by
    # itself - so it needs an extra body on the board or there's nothing to push with.
    #
    # It also needs one more swipe than the rest of the ramp. Getting a heavy sleeper
    # home costs a move that is spent pushing rather than placing, so an exact par-3
    # board with four animals and one of them immobile essentially does not exist -
    # the generator hunted for one until it ran out of budget.
    # (Heavy Sleepers' shape comes from HEAVY_ENTS / HEAVY_PAR above, which already
    # account for the pushing move — nothing extra is added here.)

    # The spikes. A longer par, one more block to reason around, and a looser dead
    # fraction so the board can actually be knotty — plus a much bigger generation
    # budget, because an exact par-7 board is far rarer than an exact par-3 one.
    budget = 25 + k * 6
    lvno = level_number(chapter, k)
    if lvno in HARD_LEVELS:
        par = 7
        walls += 1
        dead = max(dead, 0.20)
        ents = max(ents, 3)
        budget = 240
    elif lvno in MEDIUM_LEVELS:
        par = 6
        dead = max(dead, 0.12)
        ents = max(ents, 3)
        budget = 180

    return dict(w=n, h=n, walls=walls, ents=ents, par=par,
                toys=toys, dead=dead, toy=TOY[chapter],
                budget=budget)


def rules_for(chapter, toy_cells, heavy, beds, w, h, walls):
    """Build the Ctx for a chapter (1-based), given a placement."""
    kind = TOY[chapter]
    return Ctx(w, h, walls, beds,
               rugs=toy_cells if kind in ("rugs",) else
                    (toy_cells[0::2] if kind == "mixed" else ()),
               honey=toy_cells if kind in ("honey",) else
                     (toy_cells[1::2] if kind == "mixed" else ()),
               holes=toy_cells if kind == "holes" else (),
               heavy=heavy,
               sticky=(kind != "none"), anybed=(kind == "anybed"))


def gen_level(rng, chapter, sp):
    """Generate one level for a chapter, or None if the budget runs out.

    Two things every level must earn:
      * par is EXACTLY the target (no drifting to whatever the board happened to
        give), so the ramp the player feels is the ramp that was designed;
      * the chapter's toy must MATTER — take it away and the level has to become
        impossible or strictly longer. A decorative rug is a lie.
    """
    kind = sp["toy"]
    w, h, target = sp["w"], sp["h"], sp["par"]
    n_ents, n_walls = sp["ents"], sp["walls"]
    n_toys = sp["toys"]
    if kind == "holes":
        n_toys = max(2, (n_toys // 2) * 2)          # burrows only exist in pairs
    if kind == "mixed":
        n_toys = max(2, n_toys)
    if kind in ("none", "sticky"):
        n_toys = 0                                  # bare board: the rule IS the level
    deadline = time.time() + sp["budget"]
    best = None

    while time.time() < deadline:
        cells = [(x, y) for x in range(w) for y in range(h)]
        rng.shuffle(cells)
        need = n_walls + n_ents * 2 + (n_toys if kind not in ("anybed", "heavy") else 0)
        if len(cells) < need + 2:
            return None
        i = 0
        walls = set(cells[i:i + n_walls]); i += n_walls
        start = tuple(cells[i:i + n_ents]); i += n_ents
        beds = tuple(cells[i:i + n_ents]); i += n_ents
        toy_cells = ()
        if kind not in ("anybed", "heavy"):
            toy_cells = tuple(cells[i:i + n_toys]); i += n_toys
        heavy = rng.randrange(n_ents) if kind == "heavy" else -1

        ctx = rules_for(chapter, toy_cells, heavy, beds, w, h, walls)
        if any(s == b for s, b in zip(start, beds)):
            continue
        if ctx.anybed and set(start) & ctx.bedset:
            continue

        ex = explore(ctx, start, cap=90000)
        if ex is None:
            continue
        dist, came, rev, goals = ex
        if not goals:
            continue
        goal = min(goals, key=lambda g: dist[g])
        if dist[goal] != target:
            continue
        path = path_to(came, goal)
        if len(set(path)) < (2 if target <= 4 else 3):
            continue                                 # must use real direction changes
        dead = dead_fraction(ctx, dist, rev, goals)
        if dead > sp["dead"]:
            continue

        # ---- does the toy actually matter? ----
        par2 = None
        if kind == "none":
            pass            # no toy to justify — the board is the whole puzzle
        elif kind == "sticky":
            # Chapter two's twist has to be load-bearing: the level must be
            # impossible, or strictly longer, if beds DIDN'T catch and hold.
            loose = Ctx(w, h, walls, beds, sticky=False)
            par2, _, _ = solve(loose, start, cap=60000)
            if par2 is not None and par2 <= target:
                continue
        elif kind == "anybed":
            # somebody has to end up in a bed that isn't "theirs", or the chapter's
            # whole idea is doing nothing
            if all(goal[j] == beds[j] for j in range(n_ents)):
                continue
            plain = Ctx(w, h, walls, beds, sticky=True)
            par2, _, _ = solve(plain, start, cap=60000)
            if par2 is not None and par2 <= target:
                continue
        elif kind == "heavy":
            # A heavy animal ADDS a constraint, so "is it longer without?" is the
            # wrong question - it would always be shorter. What has to be true is
            # that the big one actually gets shoved: a heavy animal nobody ever
            # pushes is just a wall wearing a costume.
            pushed = False
            cur = start
            for d in path:
                nxt = slide(ctx, cur, d)
                if nxt[heavy] != cur[heavy]:
                    pushed = True
                cur = nxt
            if not pushed:
                continue
            free = Ctx(w, h, walls, beds, sticky=True)      # the big one moving normally
            par2, _, _ = solve(free, start, cap=60000)
            if par2 == target:
                continue                                    # weight changed nothing
        else:
            bare = Ctx(w, h, walls, beds, sticky=True)
            par2, _, _ = solve(bare, start, cap=60000)
            if par2 is not None and par2 <= target:
                continue

        score = -dead
        if best is None or score > best[0]:
            best = (score, dict(w=w, h=h, par=target, walls=sorted(walls),
                                start=start, beds=beds, dead=dead, path=path,
                                rugs=sorted(ctx.rugs), honey=sorted(ctx.honey),
                                holes=list(ctx.holes), heavy=heavy,
                                without=par2, chapter=chapter))
            if dead <= sp["dead"] * 0.4:
                break                                # forgiving enough, take it
    return best[1] if best else None


# ---------------------------------------------------------------- C# emission
def cells_cs(cells):
    return "new[]{ " + ",".join(f"W2({x},{y})" for x, y in cells) + " }"


def emit(lv, hint):
    walls = cells_cs(lv["walls"]) if lv["walls"] else "new Vector2Int[0]"
    ents = ", ".join(f"new EntDef({s[0]},{s[1]}, {b[0]},{b[1]})"
                     for s, b in zip(lv["start"], lv["beds"]))
    extra = ""
    if lv.get("rugs"):
        extra += f",\n                rugs: {cells_cs(lv['rugs'])}"
    if lv.get("honey"):
        extra += f",\n                honey: {cells_cs(lv['honey'])}"
    if lv.get("holes"):
        extra += f",\n                holes: {cells_cs(lv['holes'])}"
    if lv.get("heavy", -1) >= 0:
        extra += f",\n                heavy: {lv['heavy']}"
    return (f'            new Lv({lv["w"]},{lv["h"]},{lv["par"]},"{hint}",\n'
            f'                {walls},\n'
            f'                new[]{{ {ents} }}{extra}),')


# ---------------------------------------------------------------- hints
# One line per level: teach the chapter's toy in the first few, then get out of
# the way. Fifteen per chapter, matching the fifteen slots.
HINTS = {
    1: ["Swipe any direction. Everyone slides until something stops them.",
        "Walls stop you. So does the edge of the room.",
        "Two friends now. One swipe moves them both.",
        "Line them up, then send them home.",
        "Animals stop each other too - use that.",
        "A bigger room. Same one rule.",
        "Sometimes the long way round is the short way.",
        "Blocks are just walls you can plan around.",
        "Send the far one first.",
        "Corners are good places to park somebody.",
        "Three friends. Nobody gets left out.",
        "One swipe, three animals. Watch where they all end up.",
        "Use a friend as a wall for another friend.",
        "The order they stop in is the whole puzzle.",
        "Take your time. Nothing here is in a hurry.",
        "If it looks stuck, undo and try the other way.",
        "Get one home, then work on the rest.",
        "Every bed wants its own animal.",
        "Almost the end of the first room.",
        "Last one here. Then something changes."],
    2: ["Beds are sticky now. Touch yours and you're asleep for good.",
        "An animal that's asleep never moves again.",
        "A sleeping friend is a wall. That's useful.",
        "Park somebody in their bed, then use them.",
        "Who should fall asleep first?",
        "Sometimes you want to NOT land on your bed yet.",
        "Three friends and sticky beds.",
        "Wake nobody. Once they're in, they're in.",
        "Build a wall out of sleepers.",
        "The first one to bed changes everything after.",
        "Try it the other way round.",
        "Slow is fine. Undo is free.",
        "Nearly there.",
        "Four friends now. Same idea.",
        "One at a time, in the right order.",
        "The awkward one usually goes first.",
        "A sleeper in the right spot solves the rest.",
        "Think about who blocks who.",
        "Second-to-last in this room.",
        "Last one. Then the rules move again."],
    3: ["Sticky beds - but tonight nobody minds whose bed is whose.",
        "Any animal, any bed. Just fill them all.",
        "Two friends, two beds, either way round.",
        "Sometimes the far bed is the easy one.",
        "Fill the awkward bed first.",
        "Swapping who goes where can save you three swipes.",
        "Count the beds, not the animals.",
        "One bed is harder to reach than the rest. Start there.",
        "Whoever goes first decides the rest.",
        "Any order you like - but only one order is short.",
        "Leave the open bed for last.",
        "Five beds, five friends, no name tags.",
        "Look for the bed only one animal can reach.",
        "Nearly there. Fill the corner first.",
        "Every bed full, everybody asleep. That's the whole job."],
    4: ["Silk is too slippery to sleep on - you always slide back off.",
        "Cross the rug. Don't try to stop on it.",
        "A rug can carry you straight past your own bed. Careful.",
        "Use the rug to reach somewhere you couldn't stop before.",
        "Rugs turn short slides into long ones.",
        "Come at the bed from the other side.",
        "The rug is a corridor, not a room.",
        "Two rugs in a row is just a longer corridor.",
        "Stop before the silk, not on it.",
        "Sometimes the rug is the only way across.",
        "A friend parked on the far side gives you something to stop against.",
        "Plan where you'll land, not where you'll pass.",
        "Silk never lets go until something solid does.",
        "Almost the last of the rugs. Take it slowly.",
        "One room, four rugs, five sleepy animals."],
    5: ["Honey is sticky. Touch it and that's where you stay.",
        "Honey stops you dead - useful, if you aim it.",
        "Park someone in the honey on purpose.",
        "Honey beats a long slide every time.",
        "Use the honey to stop short of a bed.",
        "The honey is a brake, not a wall.",
        "Two puddles make a very short corridor.",
        "Whoever reaches the honey first blocks everyone behind.",
        "Send the wrong one into the honey and you're stuck.",
        "Honey first, beds after.",
        "A sleeper and a puddle make a pocket.",
        "Think about who must NOT touch the honey.",
        "The honey is doing half the work. Let it.",
        "Nearly the last of the mess. Mind your step.",
        "Five friends, and honey everywhere."],
    6: ["Burrows come in pairs. In one, out the other, still sliding.",
        "You keep your speed all the way through a burrow.",
        "A burrow can put you where no swipe could reach.",
        "Follow the colours - a pair shares one colour.",
        "Sometimes the long way round is underground.",
        "A friend standing on the far end blocks the burrow.",
        "Go in the near one to come out of the far one.",
        "Two pairs means two ways across.",
        "Sometimes you want to miss the burrow.",
        "The exit decides where you stop, not the entrance.",
        "Line them up before you dive.",
        "One burrow, one bed, one swipe - if you set it up right.",
        "Watch what the burrow does to the animal behind you.",
        "Nearly through. Where does that exit put you?",
        "The whole warren, all at once."],
    7: ["The big one is fast asleep. It only moves if somebody bumps it.",
        "Push the big one - it slides until something stops it.",
        "The big one makes an excellent wall.",
        "Bump it once and it's somewhere new for good.",
        "Push it out of the way before you need the space.",
        "You always stop right behind whatever you push.",
        "Line up behind the big one to move it a long way.",
        "It can be pushed into its own bed, too.",
        "Push it once too often and it's in the way.",
        "The big one is blocking the beds behind it.",
        "Decide where it has to end up first.",
        "Two pushes, if you have room for two.",
        "The big one never moves on its own. Ever.",
        "One push, then everybody home.",
        "The heaviest sleeper in the zoo, and four friends around it."],
    8: ["No new rules tonight. Everything you already know.",
        "Rug and honey in one room. Read the floor.",
        "The silk carries, the honey stops.",
        "Same rules, less room.",
        "Take one animal at a time in your head.",
        "The floor is telling you the answer.",
        "You've solved harder than this - twice.",
        "Slow down. Everything here is familiar.",
        "One awkward friend, as always.",
        "Set the room up, then send everyone home.",
        "The last few nights are the quiet ones.",
        "Nearly the end of the zoo.",
        "Second to last. Enjoy it.",
        "One more after this one.",
        "Goodnight, everybody. Sleep well."],
}


def write_cs(chapters, path=CS_PATH):
    """Splice regenerated chapters into the Levels array, leaving every chapter
    we didn't touch byte-for-byte as it was."""
    src = open(path, encoding="utf-8").read()
    head, rest = src.split("        private static readonly Lv[] Levels =\n        {\n", 1)
    body, tail = rest.split("\n        };\n", 1)

    chunks = body.split("            new Lv(")
    preamble = chunks[0].rstrip("\n")
    existing = ["            new Lv(" + c.rstrip() for c in chunks[1:]]

    out = [preamble]
    for ch in range(1, len(CHAPTER_START) + 1):
        first, count = CHAPTER_START[ch - 1], CHAPTER_LEN[ch - 1]
        if ch in chapters:
            out.append(f"\n            // ===================== CHAPTER {ch} =====================")
            for k, lv in enumerate(chapters[ch]):
                out.append(emit(lv, HINTS[ch][k]))
        else:
            for i in range(first, first + count):
                if i < len(existing):
                    out.append(existing[i])
    new = (head + "        private static readonly Lv[] Levels =\n        {\n"
           + "\n".join(out) + "\n        };\n" + tail)
    open(path, "w", encoding="utf-8", newline="\n").write(new)


def gen_slot(args):
    """One level. Top-level so a single chapter can spread its slots across cores —
    the late 7x7 slots dominate the wall clock, and they're independent of each
    other, so there's no reason to wait for them one at a time."""
    ch, k, seed = args
    made = gen_chapter((ch, seed, k, k + 1))[1]
    return k, (made[0] if made else None)


def gen_chapter(args):
    """One whole chapter, or a slice of one. Top-level so it can run in its own
    process. `args` is (chapter, seed) or (chapter, seed, first_slot, last_slot)."""
    ch, seed = args[0], args[1]
    lo = args[2] if len(args) > 2 else 0
    hi = args[3] if len(args) > 3 else CHAPTER_LEN[ch - 1]
    rng = random.Random(seed)
    made = []
    for k in range(lo, hi):
        base = spec_for(ch, k)
        lv = None
        # A ladder of retries, each giving up something the player won't notice —
        # never the par, which IS the ramp. The late 7x7 slots are the hard ones:
        # an exact par on a big board with a toy that has to matter is a narrow
        # target, so the last rungs shrink the room rather than abandoning a
        # chapter that already has fourteen good levels in it.
        # It goes BOTH ways. The original ladder only ever removed walls and shrank
        # the board, which is the right medicine for a crowded 7x7 and exactly the
        # wrong medicine for a bare 4x4 — a tiny empty board has so few reachable
        # states that an exact par is often unreachable, and the fix is more
        # structure, not less. Chapter one ground to a halt on that.
        ladder = [
            dict(),
            dict(dead=min(0.55, base["dead"] + 0.06), budget=base["budget"] + 20),
            dict(walls=base["walls"] + 1, dead=min(0.55, base["dead"] + 0.08),
                 budget=base["budget"] + 30),
            dict(dead=min(0.55, base["dead"] + 0.12), toys=base["toys"] + 1,
                 budget=base["budget"] + 40),
            dict(w=base["w"] + 1, h=base["h"] + 1, walls=base["walls"] + 1,
                 dead=min(0.55, base["dead"] + 0.14), budget=base["budget"] + 60),
            dict(dead=min(0.55, base["dead"] + 0.12), walls=max(0, base["walls"] - 1),
                 toys=base["toys"] + 1, budget=base["budget"] + 60),
            dict(w=max(4, base["w"] - 1), h=max(4, base["h"] - 1),
                 dead=min(0.55, base["dead"] + 0.18), budget=base["budget"] + 60),
            dict(w=base["w"] + 1, h=base["h"] + 1, walls=base["walls"] + 2,
                 toys=base["toys"] + 1, dead=0.55, budget=base["budget"] + 90),
            # Last resort: one fewer animal. It's the thing a player is least likely to
            # notice between two neighbouring levels, and it widens the target enormously
            # — an exact par with five animals AND a heavy one that must actually get
            # pushed is a very narrow needle, and chapter seven kept missing it.
            dict(ents=max(2, base["ents"] - 1), dead=min(0.55, base["dead"] + 0.14),
                 budget=base["budget"] + 90),
            dict(ents=max(2, base["ents"] - 1), walls=base["walls"] + 1,
                 w=base["w"] + 1, h=base["h"] + 1, dead=0.55, budget=base["budget"] + 120),
        ]
        for rung in ladder:
            lv = gen_level(rng, ch, dict(base, **rung))
            if lv:
                break
        if not lv:
            print(f"// FAILED ch{ch} slot {k+1} ({base['w']}x{base['h']}, "
                  f"{base['ents']} animals, par {base['par']}) - keeping the "
                  f"{len(made)} levels made so far", flush=True)
            return ch, made
        made.append(lv)
        print(f"// ch{ch} lv{k+1:2d}: {lv['w']}x{lv['h']} "
              f"animals={len(lv['start'])} par={lv['par']} dead={lv['dead']:.2f} "
              f"without={lv['without']} "
              f"line={''.join(DIRNAME[d][0] for d in lv['path'])}", flush=True)
    return ch, made


def generate_plan(seed, want):
    import json
    from concurrent.futures import ProcessPoolExecutor
    chapters = {}
    with ProcessPoolExecutor(max_workers=min(6, len(want))) as pool:
        for ch, made in pool.map(gen_chapter, [(c, seed + c * 7919) for c in want]):
            if len(made) == CHAPTER_LEN[ch - 1]:
                chapters[ch] = made
                # save after every chapter: a run this long shouldn't be all-or-nothing
                with open(PLAN_JSON, "w", encoding="utf-8") as f:
                    json.dump({str(k): v for k, v in chapters.items()}, f, default=list, indent=1)
                print(f"// chapter {ch} done and saved", flush=True)
            else:
                print(f"// chapter {ch} incomplete ({len(made)}) - not written", flush=True)
    if chapters:
        with open(PLAN_JSON, "w", encoding="utf-8") as f:
            json.dump({str(k): v for k, v in chapters.items()}, f, default=list, indent=1)
        write_cs(chapters)
        print(f"\nwrote chapters {sorted(chapters)} into {CS_PATH}")
    return chapters


# ---------------------------------------------------------------- verification
def parse_levels(path=CS_PATH, array="Levels"):
    import re
    src = open(path, encoding="utf-8").read()
    body = src.split(f"private static readonly Lv[] {array} =", 1)[1]
    body = body.split("\n        };", 1)[0]
    out = []
    for chunk in body.split("new Lv(")[1:]:
        head = re.match(r"\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*,", chunk)
        w, h, par = (int(g) for g in head.groups())
        ents = [tuple(int(g) for g in m) for m in
                re.findall(r"new EntDef\((\d+),(\d+),\s*(\d+),(\d+)\)", chunk)]

        def named(tag):
            m = re.search(tag + r":\s*new\[\]\{([^}]*)\}", chunk)
            if not m:
                return []
            return [(int(a), int(b)) for a, b in re.findall(r"W2\((\d+),(\d+)\)", m.group(1))]

        upto = len(chunk)
        for tag in ("rugs:", "honey:", "holes:", "heavy:"):
            k = chunk.find(tag)
            if k >= 0:
                upto = min(upto, k)
        walls = [(int(a), int(b)) for a, b in re.findall(r"W2\((\d+),(\d+)\)", chunk[:upto])]
        hv = re.search(r"heavy:\s*(\d+)", chunk)
        out.append(dict(w=w, h=h, par=par, ents=ents, walls=walls,
                        rugs=named("rugs"), honey=named("honey"),
                        holes=named("holes"), heavy=int(hv.group(1)) if hv else -1))
    return out


def chapter_of(i):
    for c in range(len(CHAPTER_START) - 1, -1, -1):
        if i >= CHAPTER_START[c]:
            return c + 1
    return 1


def ctx_for_level(lv, chapter):
    beds = tuple((e[2], e[3]) for e in lv["ents"])
    return Ctx(lv["w"], lv["h"], set(lv["walls"]), beds,
               rugs=lv["rugs"], honey=lv["honey"], holes=lv["holes"],
               heavy=lv["heavy"], sticky=chapter >= 2, anybed=(chapter == 3))


def audit(path=CS_PATH):
    levels = parse_levels(path)
    ok = True
    for i, lv in enumerate(levels):
        ch = chapter_of(i)
        ctx = ctx_for_level(lv, ch)
        start = tuple((e[0], e[1]) for e in lv["ents"])
        problems = []
        if len(set(start)) != len(start):
            problems.append("two animals on one cell")
        if set(lv["walls"]) & (set(start) | set(ctx.beds)):
            problems.append("something standing on a wall")
        toy = set(lv["rugs"]) | set(lv["honey"]) | set(lv["holes"])
        if toy & (set(start) | set(ctx.beds) | set(lv["walls"])):
            problems.append("a toy sharing a cell with a bed, an animal or a wall")
        got, _, _ = solve(ctx, start, cap=200000)
        if got != lv["par"]:
            problems.append(f"par is {got}, file says {lv['par']}")
        if problems:
            ok = False
        print(f'{"OK " if not problems else "BAD"} level {i+1:3d} [ch{ch}] '
              f'{lv["w"]}x{lv["h"]} {len(lv["ents"])} animals par={lv["par"]}'
              + (("  <-- " + "; ".join(problems)) if problems else ""))
    print(f"\n{len(levels)} levels checked - "
          + ("all pars are BFS-optimal" if ok else "PROBLEMS FOUND"))
    return ok


def selftest():
    """Tiny hand-checked boards, one per rule, so a mistake in the simulation
    shows up here instead of buried in ninety generated levels."""
    fails = []

    def check(name, got, want):
        if got != want:
            fails.append(f"{name}: got {got}, expected {want}")

    # plain slide: everyone runs to the wall, in order
    c = Ctx(4, 4, set(), ((0, 0), (0, 1)))
    check("plain right", slide(c, ((0, 0), (2, 0)), (1, 0)), ((2, 0), (3, 0)))
    # sticky: caught by its own bed in passing
    c = Ctx(4, 4, set(), ((2, 0),), sticky=True)
    check("sticky catch", slide(c, ((0, 0),), (1, 0)), ((2, 0),))
    # chapter 1: no stickiness, so it slides straight past the bed
    c = Ctx(4, 4, set(), ((2, 0),))
    check("not sticky", slide(c, ((0, 0),), (1, 0)), ((3, 0),))
    # rug: can't come to rest on silk, so it backs up to the last solid floor
    c = Ctx(4, 4, set(), ((0, 3),), rugs={(3, 0)}, sticky=True)
    check("rug backs up", slide(c, ((0, 0),), (1, 0)), ((2, 0),))
    # honey: stops the instant it's touched
    c = Ctx(4, 4, set(), ((0, 3),), honey={(2, 0)}, sticky=True)
    check("honey stops", slide(c, ((0, 0),), (1, 0)), ((2, 0),))
    # burrow: in at (1,0), out at (3,3), then keeps sliding to the wall
    c = Ctx(4, 4, set(), ((0, 3),), holes=((1, 0), (3, 3)), sticky=True)
    check("burrow", slide(c, ((0, 0),), (1, 0)), ((3, 3),))
    # heavy: nothing touching it, so it doesn't move at all
    c = Ctx(4, 4, set(), ((0, 3), (1, 3)), heavy=1, sticky=True)
    check("heavy stays put", slide(c, ((0, 0), (2, 0)), (1, 0)), ((2, 0), (3, 0)))
    # ...but pushed, it slides on, and the pusher stops right behind it
    check("heavy gets pushed", slide(c, ((0, 0), (1, 0)), (1, 0)), ((2, 0), (3, 0)))
    # any bed: an animal is caught by ANY bed, and the goal is "every bed filled"
    c = Ctx(4, 4, set(), ((2, 0), (0, 3)), sticky=True, anybed=True)
    check("any bed catches", slide(c, ((0, 0),), (1, 0)), ((2, 0),))
    c = Ctx(4, 4, set(), ((3, 0), (0, 0)), sticky=True, anybed=True)
    check("any bed goal", c.goal(((0, 0), (3, 0))), True)
    check("any bed not done", c.goal(((0, 0), (1, 0))), False)

    for f in fails:
        print("FAIL", f)
    print("selftest: every rule behaves" if not fails else f"selftest: {len(fails)} FAILURES")
    return not fails


def gen_round(ch, slots, seed, workers=7):
    """Generate the given slots of a chapter, one per core. Returns {slot: level}
    for whichever ones came out — never all-or-nothing."""
    from concurrent.futures import ProcessPoolExecutor
    jobs = [(ch, k, seed + k * 104729) for k in slots]
    out = {}
    with ProcessPoolExecutor(max_workers=min(workers, len(jobs))) as pool:
        for k, lv in pool.map(gen_slot, jobs):
            if lv is None:
                print(f"// FAILED ch{ch} slot {k+1}", flush=True)
            else:
                out[k] = lv
                print(f"// ch{ch} lv{k+1:2d}: {lv['w']}x{lv['h']} "
                      f"animals={len(lv['start'])} par={lv['par']} "
                      f"dead={lv['dead']:.2f} without={lv['without']}", flush=True)
    return out


def fill_chapter(ch, seed=20260801, rounds=8, workers=7):
    """Generate one missing chapter and merge it into the saved plan.

    Resumable on purpose. Every finished level is checkpointed to disk the moment
    its round ends, and later rounds re-roll ONLY the slots still missing, each
    with a fresh seed. Throwing away thirteen good levels because the fourteenth
    was stubborn is exactly how the chapter-5 hole happened; it can't happen twice.
    """
    import json
    import os
    n = CHAPTER_LEN[ch - 1]
    cache = os.path.join(os.path.dirname(PLAN_JSON) or ".", f".ch{ch}_partial.json")
    done = {}
    if os.path.exists(cache):
        done = {int(k): v for k, v in json.load(open(cache, encoding="utf-8")).items()}
        print(f"// resuming chapter {ch}: {len(done)}/{n} already made", flush=True)

    for r in range(rounds):
        missing = [k for k in range(n) if k not in done]
        if not missing:
            break
        print(f"// ch{ch} round {r+1}: {len(missing)} slot(s) to go "
              f"({', '.join(str(k+1) for k in missing)})", flush=True)
        done.update(gen_round(ch, missing, seed + ch * 7919 + r * 1000003, workers))
        with open(cache, "w", encoding="utf-8") as f:
            json.dump({str(k): v for k, v in done.items()}, f, default=list)

    if len(done) < n:
        print(f"chapter {ch} still incomplete ({len(done)}/{n})")
        return False
    plan = {}
    if os.path.exists(PLAN_JSON):
        plan = json.load(open(PLAN_JSON, encoding="utf-8"))
    plan[str(ch)] = [done[k] for k in sorted(done)]
    with open(PLAN_JSON, "w", encoding="utf-8") as f:
        json.dump(plan, f, default=list, indent=1)
    os.remove(cache)
    print(f"chapter {ch} complete and merged into the plan")
    return True


# ------------------------------------------------------------- nightly puzzles
# The pool Tonight's Puzzle draws from. Chapter one's rules ONLY (chapter 0 in the
# TOY table), because a daily puzzle is the one level a brand-new player might tap
# first, and it must never rely on — or give away — a twist they haven't reached.
#
# Pars stop at 5. A nightly puzzle is a nightcap, not a project: it has to fit in
# the last few minutes before somebody puts the phone down, on a day they might
# already be too tired to think.
DAILY_PATH = "Assets/Scripts/SleepyZoo/DailyLevels.cs"
# board, animals, par, walls.
#
# Capped at THREE animals on purpose. Chapter one has no sticky beds, so every
# animal has to be on its own bed at the same instant — nothing stays put. Four
# animals makes that a coincidence the generator can almost never manufacture at an
# exact par, which is why the first pass produced nothing above three. Difficulty
# here comes from the board and the par, the same rule the campaign follows.
DAILY_ROWS = [
    (5, 2, 3, 1), (5, 2, 4, 1), (5, 2, 4, 2), (5, 3, 4, 2),
    (5, 3, 4, 2), (5, 3, 5, 2), (6, 2, 4, 2), (6, 3, 5, 3),
    (6, 3, 5, 3), (6, 3, 4, 3), (6, 2, 4, 2), (6, 3, 5, 3),
]

DAILY_HINTS = [
    "Tonight's puzzle. Same one for everybody.",
    "One board, one bedtime.",
    "A quiet one to end the day on.",
    "Everyone gets this exact puzzle tonight.",
    "Take your time. It'll keep.",
    "A small one before lights out.",
]


def gen_daily_one(args):
    """One nightly level. Top-level so the pool can be built across cores."""
    k, seed = args
    n, ents, par, walls = DAILY_ROWS[k % len(DAILY_ROWS)]
    sp = dict(w=n, h=n, walls=walls, ents=ents, par=par, toys=0,
              # Forgiving on purpose: a daily puzzle you can wedge into an unwinnable
              # state is a daily puzzle people stop opening.
              dead=0.10, toy="none", budget=60)
    return k, gen_level(random.Random(seed), 0, sp)


DAILY_CACHE = "tools/.dailies_partial.json"


def gen_dailies(count=72, seed=20260801, workers=7, rounds=6):
    """Build the nightly pool and write DailyLevels.cs.

    Resumable, and happy with less than it asked for: a pool of 50 good nightly
    puzzles is a better product than a crash, and the pool cycles anyway."""
    import json
    import os
    from concurrent.futures import ProcessPoolExecutor
    made = {}
    if os.path.exists(DAILY_CACHE):
        made = {int(k): v for k, v in json.load(open(DAILY_CACHE, encoding="utf-8")).items()}
        print(f"// resuming with {len(made)} nightly puzzles already made", flush=True)

    for r in range(rounds):
        missing = [k for k in range(count) if k not in made]
        if not missing:
            break
        print(f"// dailies round {r+1}: {len(missing)} to go", flush=True)
        jobs = [(k, seed + k * 104729 + r * 7919) for k in missing]
        got = 0
        with ProcessPoolExecutor(max_workers=min(workers, len(jobs))) as pool:
            for k, lv in pool.map(gen_daily_one, jobs):
                if lv:
                    made[k] = lv; got += 1
                    print(f"// daily {k+1:3d}: {lv['w']}x{lv['h']} "
                          f"animals={len(lv['start'])} par={lv['par']} "
                          f"dead={lv['dead']:.2f}", flush=True)
        with open(DAILY_CACHE, "w", encoding="utf-8") as f:
            json.dump({str(k): v for k, v in made.items()}, f, default=list)
        if got == 0:
            print("// a whole round produced nothing — taking the pool as it stands",
                  flush=True)
            break

    pool_levels = [made[k] for k in sorted(made)]
    if len(pool_levels) < 20:
        sys.exit(f"only {len(pool_levels)} nightly levels — not enough for a pool")

    for lv in pool_levels:
        lv["start"] = [tuple(s) for s in lv["start"]]
        lv["beds"] = [tuple(b) for b in lv["beds"]]
        lv["walls"] = [tuple(c) for c in lv.get("walls") or []]
    body = "\n".join(emit(lv, DAILY_HINTS[i % len(DAILY_HINTS)])
                     for i, lv in enumerate(pool_levels))

    # Replace whatever sits between the array's `{` and its closing `};` — matching
    # on the exact whitespace of an EMPTY array is how this crashed the first time.
    src = open(DAILY_PATH, encoding="utf-8").read()
    opener = "        private static readonly Lv[] Dailies =\n        {\n"
    head, rest = src.split(opener, 1)
    close = rest.index("        };\n")
    tail = rest[close + len("        };\n"):]
    open(DAILY_PATH, "w", encoding="utf-8", newline="\n").write(
        head + opener + body + "\n        };\n" + tail)
    print(f"\nwrote {len(pool_levels)} nightly puzzles to {DAILY_PATH}")
    return len(pool_levels)


def audit_dailies():
    """Re-solve every nightly puzzle. Same guarantee as the campaign: par is the
    true optimum, so 3 stars is always actually reachable."""
    levels = parse_levels(DAILY_PATH, array="Dailies")
    bad = 0
    for i, lv in enumerate(levels):
        start = tuple((e[0], e[1]) for e in lv["ents"])
        beds = tuple((e[2], e[3]) for e in lv["ents"])
        # chapter one's rules, deliberately: no sticky beds, no toys
        ctx = Ctx(lv["w"], lv["h"], set(lv["walls"]), beds, sticky=False)
        par, _, _ = solve(ctx, start, cap=200000)
        if par != lv["par"]:
            bad += 1
            print(f"BAD nightly {i+1}: stored par={lv['par']} but solver says {par}")
    print(f"\n{len(levels)} nightly puzzles checked - "
          + ("all pars are BFS-optimal" if not bad else f"{bad} DISAGREE"))
    return bad == 0


if __name__ == "__main__":
    cmd = sys.argv[1] if len(sys.argv) > 1 else "audit"
    if cmd in ("audit", "verifycs"):
        sys.exit(0 if audit() else 1)
    elif cmd == "selftest":
        sys.exit(0 if selftest() else 1)
    elif cmd == "dailies":
        gen_dailies(int(sys.argv[2]) if len(sys.argv) > 2 else 72)
    elif cmd == "auditdailies":
        sys.exit(0 if audit_dailies() else 1)
    elif cmd == "fill":
        sys.exit(0 if fill_chapter(int(sys.argv[2])) else 1)
    elif cmd == "plan":
        rest = [int(a) for a in sys.argv[2:]]
        seed = next((a for a in rest if a > 100), 20260801)
        chs = [a for a in rest if a <= 8] or [3, 4, 5, 6, 7, 8]
        generate_plan(seed, chs)
    else:
        print(__doc__)
