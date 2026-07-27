# PROJECT_MEMORY.md

> **Audience:** AI coding agents and future maintainers, for long-term product
> context. This file is meant to stay **stable** — vision, target users,
> constraints, naming, and roadmap don't change week to week. For what's
> actively being worked on right now (current TODOs, uncommitted state, dead
> code, build commands), see `/AI_CONTEXT.md` in the repo root instead — that
> file is expected to change frequently and should be read first for any
> hands-on-keyboard task.

---

## Vision

A cozy, low-stress mobile puzzle game built around one clean, ownable
mechanic — not a genre clone with an animal skin. The explicit product thesis,
carried across this project's entire history, is:

> **Theme is not a hook. Differentiation must be mechanical.** A cute-animal
> skin on top of an existing genre (match-3, merge, Flow Free, physics
> stacking) is a clone every time, however nice the art is.

The game's target feeling is "tuck sleepy animals into bed" — warm, night-time,
bedtime-story aesthetic (moons, fireflies, soft lantern light, cream/honey
color palette) paired with a genuinely novel puzzle mechanic that a casual
player can learn in one level and a puzzle fan can respect.

## Target users

- Casual mobile puzzle players who enjoy games like **Flow Free**, **Sudoku**,
  or sliding-block puzzles (Rush Hour-style) in short bedtime/commute sessions.
- Players drawn in by cozy/cute aesthetics (animals, soft color palettes,
  gentle music) rather than competitive or twitch mechanics.
- Explicitly **not** chasing the hyper-casual/viral UA-spend audience — see
  Business constraints below.

## Business constraints / strategic stance

- **No ad-spend lottery.** The stated strategy is depth + word-of-mouth (the
  "Balatro / Vampire Survivors model" — a small, deep, distinctive game that
  spreads by being genuinely good), not hyper-casual viral acquisition, which
  a solo developer can't realistically win against studios with UA budgets.
- **Solo/small-team constraint.** All tooling choices (procedural art
  fallbacks, code-built scenes, no external level-editor) reflect a project
  built and maintained by a very small team — prefer approaches that don't
  require a dedicated artist, level designer, or QA team to keep moving.
- **No monetization implemented yet, on purpose.** No ads, no IAP, no
  accounts, no backend, no analytics. This is a deliberate sequencing choice —
  prove the core loop and get it fun first — not an oversight. Do not add
  monetization/SDKs without an explicit product decision to do so.
- **Play Store is the target distribution channel** (Android `applicationId`
  is already reserved: `com.wobblegames.wobblezoo`). No iOS work has been
  done. No store listing exists yet — the game has never shipped publicly.

## Naming decisions

- **Working title: "Wobble Zoo."** Company name "Wobble Games." Both are
  **historical carryovers from an earlier physics-based prototype** (a
  tilt/wobble merge game, then a "Wobble Tower" stacking game) that no longer
  describes the current game — the current mechanic has nothing to do with
  wobbling or towers. The name has been flagged internally as a likely rename
  candidate but **no replacement name has been chosen or committed**. Do not
  assume "Wobble Zoo" is the final shipped name; do not assume it needs to
  change either — this is an open decision for the product owner.
- **In-code / commit-message nickname: "Sleepy Zoo"** and **"Bedtime
  Shuffle"** have both been used informally to describe the current
  animals-in-beds mechanic. Neither is a confirmed final title. Treat all
  three names ("Wobble Zoo", "Sleepy Zoo", "Bedtime Shuffle") as internal
  code-names until the product owner picks one for the store listing.
- **Android package ID (`com.wobblegames.wobblezoo`) should be treated as
  effectively permanent** regardless of what display name is chosen — changing
  it post-launch breaks update continuity on the Play Store. A display-name
  rename does not require changing the package ID.

## UI conventions

- **Visual language:** warm/cozy "bedtime" palette — cream/honey board tiles,
  deep night-sky gradient with a soft moon glow as the universal background,
  soft rounded shapes everywhere (no hard edges or sharp UI chrome), gentle
  glow/halo effects rather than flat icons for emphasis (see bed color-coding
  in `PuzzleGame.cs`, where each animal's bed glows in that animal's
  signature color so "whose bed is whose" reads instantly).
- **Typography:** a single font, **Fredoka (SemiBold)**, across the entire
  game. Do not introduce a second font family.
- **UI framework:** IMGUI (`OnGUI`) throughout, not UGUI/Canvas or UI Toolkit.
  Buttons are large, high-contrast, brown text on a warm pill shape, sized
  generously for thumb reach (a repeated, explicit note across commit history
  is "buttons should be big and obviously tappable" — this has been revisited
  and enlarged more than once; treat "make buttons bigger, not smaller" as the
  default instinct if in doubt).
- **Feedback:** every tap gets an audio blip (currently procedural, see
  `Sfx.cs`) and, on menu buttons, a small squash/bounce animation
  (`MenuButton.cs`). New interactive elements should follow this pattern
  rather than being silent/static.
- **Progress signaling:** stars (1–3 per level) are the core progress
  currency, both per-level (move-efficiency reward) and cumulative (gates
  level access). Any new progression system added later should probably slot
  into this existing star economy rather than introducing a second currency,
  to avoid diluting what stars mean to the player.

## Mechanic history (why the game looks the way it does)

Understanding *why* today's mechanic exists requires knowing what was tried
and rejected. This project has pivoted its core mechanic **twice**:

1. **Tilt/merge-in-a-jar** (earliest prototype, `aa193a9`) — a tilt-to-play
   merge game. Rejected: too close to existing merge-game genre conventions.
2. **"Wobble Tower"** (`97b433d`) — merge + physics stacking + tilt-to-balance.
   Rejected: physics-based stacking is unreliable/flaky for a puzzle game and
   still not mechanically distinct enough. (This is also where the "Wobble"
   branding originated, which now outlives the mechanic it was named for.)
3. **"Animals-as-mechanic"** (`77360d6` → `d6f4839` → refined through
   `5521a06`, `d3c498d`) — each animal species was bound to one unique,
   deterministic movement rule (step one tile / roll-until-blocked / hop two
   tiles / push a neighbor / fly over obstacles / passive-only-when-pushed),
   and the puzzle was to route each animal to its own bed using its rule.
   This was a real improvement (deterministic, no physics) but still risked
   reading as "Flow Free with reskinned rules" per internal critique.
4. **"Bedtime Shuffle" (current, `ac86eb8` onward)** — the mechanic was
   simplified and sharpened: **one swipe direction slides every animal on the
   board simultaneously**, each one sliding until it hits a wall, the board
   edge, or another animal (a Rush-Hour/sliding-block lineage, not a Flow-Free
   one). This is the current, standing mechanic. It has not been rejected and
   should be treated as settled unless the product owner explicitly reopens
   the mechanic question again.

**Lesson worth preserving:** each rejected mechanic wasn't rejected for
execution quality — each was built reasonably well — but for not being
*mechanically* distinct enough from an existing genre. Any future mechanic
proposal should be pressure-tested against that same bar before implementation
begins, not after.

## Roadmap (directional, not committed dates)

Ideas that have been discussed or partially scaffolded but are **not**
committed work, listed roughly by how much groundwork already exists:

- **Chapter 3 and beyond** — the campaign is now **two 20-level chapters**
  (40 levels), and the shape is set: each chapter takes one rule the previous
  chapter taught as immovable and breaks it. Chapter 1 = plain beds you slide
  off; chapter 2 = **sticky beds** (touch your own bed, even mid-slide, and you
  sleep there for good, becoming a wall). The menu deliberately hides a locked
  chapter's name and twist — "? ? ?" plus a star cost — because the reveal is
  the retention hook. `tools/gen_levels.py` now generates and BFS-verifies
  levels for any chapter rule, so expanding is no longer hand-authoring.
  Candidate chapter-3 twists (unbuilt): wrap-around edges, beds that move,
  animals that wake each other.
- **Real audio** — procedural blips are a placeholder. Sourced audio (Kenney
  interface SFX + music) was acquired at one point per `docs/ASSETS_AND_TODO.md`
  but was never wired in, and that doc predates the current mechanic — treat
  this as "worth revisiting," not "ready to wire in as-is."
- **A proper level-select map** (winding path with node markers, per early
  planning docs) was considered and then **superseded** by a simpler grid
  panel — the map idea is explicitly not currently planned; don't resurrect it
  without checking with the product owner, since the grid picker was a
  deliberate simplification, not a placeholder for the map.
- **A collection/gallery screen** for the animal cast was discussed early on,
  never built, no current plan to build it.
- **Monetization, accounts, analytics, backend** — all explicitly deferred
  until the core loop is proven fun. No SDKs chosen yet.
- **Final naming decision** (see Naming decisions above) — open.
- **iOS port** — never scoped; Android/Play Store only so far.

---

*Keep this file focused on things that would still be true in six months. If
you're about to add a note that will be stale after the next commit, it
probably belongs in `/AI_CONTEXT.md` instead.*
