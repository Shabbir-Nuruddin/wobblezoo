# AI_CONTEXT.md

> **Audience:** AI coding agents (Claude, GPT, Gemini, etc.), not humans.
> Read this file fully before touching code. It changes frequently as work
> progresses — treat it as the current source of truth, more current than any
> commit message or docs/ file. If this file and `docs/ASSETS_AND_TODO.md`
> disagree, **trust this file** (see Known Gotchas).

---

## Project Identity

- **Name:** Wobble Zoo (Android applicationId `com.wobblegames.wobblezoo`; company "Wobble Games")
- **Purpose:** A cozy, deterministic mobile puzzle game for the Google Play Store — swipe to slide sleepy animals into their beds.
- **Status:** Prototype / pre-release. No store listing yet. Core loop, **40 levels across 2 chapters**, menu, and level-select are built and building cleanly. Never shipped to Play Store.

---

## Architecture Snapshot

- **Engine/framework:** Unity **6000.4.11f1** (Unity 6 LTS-track editor). C# scripting, IMGUI (`OnGUI`) for all UI — no UGUI Canvas, no UI Toolkit.
- **Runtime target:** Android (min SDK 24 / Android 7.0), IL2CPP, ARM64. A temporary Windows Standalone build path also exists for local screenshotting.
- **Package manager:** Unity Package Manager (`Packages/manifest.json`) — stock Unity modules only, no third-party packages, no npm/pip involved in the game itself.
- **Important directories:**
  - `Assets/Scripts/SleepyZoo/` — the puzzle game itself. **Only one file: `PuzzleGame.cs`** (~845 lines). This is the core gameplay.
  - `Assets/Scripts/ChonkyMerge/` — everything else runtime: `MainMenu.cs` (landing page + level picker), `MenuButton.cs`, `Sfx.cs` (procedural audio), `NativeShare.cs` (Android share sheet). Also contains **dead code** — see Known Gotchas.
  - `Assets/Editor/` — editor-only build tooling, run via `-executeMethod` in batch mode (see Environment Requirements): `PuzzleSceneBuilder.cs`, `ApkBuilder.cs`, `StandaloneBuilder.cs`, `ArtImportSettings.cs`.
  - `Assets/Scenes/` — `MainMenu.unity` and `Puzzle.unity`. Both scenes are built entirely from code at `Start()` — there is no manual GameObject wiring to preserve; the scenes just hold one empty GameObject with the relevant MonoBehaviour attached.
  - `Assets/Resources/Art/` — all sprites, loaded at runtime via `Resources.Load`. Subfolder `Art/pets/` holds the 10 sprites the current puzzle uses (`dog, rabbit, panda, owl, pig, frog, penguin, bear, duck, cow`). Subfolder `Art/animals/` (`tier1..tier14`) is **leftover from a retired mechanic** — see Known Gotchas.
  - `Assets/Resources/Fonts/Fredoka.ttf` — the game's only font.
  - `_ArtSource/` — git-ignored. Raw/original art, copyrighted reference images (never shipped), and pending (not-yet-used) generated art. Do not expect this folder to exist on a fresh clone.
  - `docs/` — two Markdown handoff docs, both **stale** (predate the current mechanic). See Known Gotchas.
  - `tools/gen_levels.py` — **the level generator and verifier.** Mirrors `SlideSim` for both chapter rule sets; `verifycs` re-proves every par in `PuzzleGame.cs`. Run it after touching the `Levels` array.
  - `tools/process_animals.py` — a Python art-processing script for the retired `Art/raw` → `Art/animals` pipeline. Its input folder (`Assets/Resources/Art/raw/`) no longer exists in the repo. Effectively dead/historical.
  - `Builds/` — git-ignored build output (`WobbleZoo.apk`, `Win/`).
- **Database/storage:** None. All persistence is `PlayerPrefs` (local device key-value store) — see keys list below.
- **External services/APIs:** None. No backend, no analytics SDK wired in, no ads SDK, no IAP. `NativeShare.cs` calls the OS share sheet only (no network call).

---

## Current Working State

### What was being worked on most recently (per git log, newest first)
`a679ae9` "Helpful solver-hints, gentler 16-level ramp, big Levels button" — the most recent commit on `master`. It followed a full mechanic pivot in `ac86eb8` ("Pivot to Bedtime Shuffle: one-swipe-slides-everyone mechanic").

### What is already completed
- **Core mechanic ("Bedtime Shuffle"):** one swipe direction slides **every** animal at once, each one sliding until it hits a wall/edge/another animal (see `SlideSim` in `Assets/Scripts/SleepyZoo/PuzzleGame.cs:436`). This replaced an earlier "each animal has its own movement rule" mechanic entirely — that old system (Step/Roll/Hop/Push/Sleep/Fly per animal, tier-based `GameConfig`) is gone from gameplay.
- **40 levels in two 20-level chapters**, each BFS-verified with `par` = true optimal move count (`Levels` array in `PuzzleGame.cs`). **Chapter 2 changes the rule**: beds become *sticky* — an animal that touches its own bed, even mid-slide, stops there and never moves again (it becomes a wall for everyone else). This is two `_sticky`-guarded lines inside `SlideSim`; the solver, hints and stars all inherit it automatically. Every chapter-2 level is *unsolvable* under chapter-1 rules (enforced by the generator), so the twist is load-bearing, not decorative.
- **`tools/gen_levels.py`** — the level generator/verifier. It mirrors `SlideSim` exactly for both rule sets, derives `par` from BFS, rejects levels where the optimal solution uses too few distinct directions, and measures a "dead fraction" (share of reachable states from which the goal is no longer reachable) to keep levels forgiving. `python tools/gen_levels.py verify` re-proves the Python sim matches the C# one against the shipped chapter-1 levels — **run it before trusting any generated level**.
- **In-game optimal-move hint solver** (`SolveFrom`, `PuzzleGame.cs:486`) — reused for both the level-1 tutorial demo arrow and the in-play "Need a hint?" button. Same BFS also used for design-time level validation.
- **Star economy with checkpoints**: 1–3 stars per level (3★ = `par`, 2★ = `TwoStarMoves(par)` which widens with level length); a running total gates every 4th level behind a cumulative star threshold (`Gates` / `RequiredStars` / `IsUnlocked`). The big one is **level 21 = 36 of a possible 60 stars**, which is the door into chapter 2. 120 stars total.
- **Chapter reveal as the retention hook**: the level picker shows a locked chapter as `Chapter 2 — ? ? ?` with only a tease ("One bedtime rule you know by heart is about to change") and the star cost. Clearing level 20 shows a "Chapter complete!" panel whose button reads **See what changed**. Level 21 gets its own guided-arrow tutorial (`TaughtKey(chapter)`), same as level 1.
- **Main menu**: background, bobbing floater critters, logo, Play/Settings buttons, sound toggle, share button, and a **Levels** picker panel showing a 4-column grid with per-level stars and lock/gate state (`Assets/Scripts/ChonkyMerge/MainMenu.cs`).
- **Fully procedural visuals for the puzzle screen** — board tiles, bed glow rings, arrow, button pill fallback, background gradient/moon are all generated in code (`RoundedTile`, `SoftDisc`, `ArrowSprite`, `BgGradient`, `MakeButtonTex` in `PuzzleGame.cs`), so the puzzle scene has minimal art dependencies.
- **Android APK build path** verified working headlessly (see Environment Requirements).

### What is partially implemented
- **Windows Standalone build** (`Assets/Editor/StandaloneBuilder.cs`) exists only for local screenshotting/testing; the file's own header comment calls it "TEMPORARY... Safe to delete."
- **Audio**: only procedural click/pop blips (`Sfx.cs`). Real audio files were received per `docs/ASSETS_AND_TODO.md` (Kenney SFX + music) but were **never wired in** — status of those source files is unknown post-pivot (they lived in `_ArtSource/audio_pending/`, which is git-ignored and not verifiable from the repo alone).
- **Sharing copy** is hardcoded and generic (`MainMenu.cs:215`) — not tested on a real device.

### What is intentionally not implemented
- No level-select "map" screen (winding path with node markers) — an earlier plan (see `docs/ASSETS_AND_TODO.md`) was superseded by the simpler grid-panel `Levels` button in the current menu. Do not resurrect the map plan without checking with the product owner first.
- No collection/gallery screen for animals.
- No monetization (ads/IAP), no analytics, no cloud save, no accounts.
- No automated tests (no test framework is set up in this Unity project at all).

---

## Active Problems / TODOs

### 1. Two stale planning docs contradict the current game
- **Problem:** `docs/ASSETS_AND_TODO.md` and `docs/ART_PROMPTS.md` describe the **retired** per-animal-ability mechanic (cat steps, hamster rolls, corgi pushes, etc.), reference a level-select map and collection screen that were abandoned, and point at scripts/folders that no longer exist (`scratchpad/solve_levels.py`, `Assets/Resources/Art/raw/`).
- **Suspected cause:** Docs were written before the "Bedtime Shuffle" pivot (`ac86eb8`) and never updated afterward.
- **Files involved:** `docs/ASSETS_AND_TODO.md`, `docs/ART_PROMPTS.md`.
- **Recommended next step:** Either delete both files or rewrite them to match current reality; until then, an agent reading them cold **will** propose reverting/duplicating work that's already done differently. This `AI_CONTEXT.md` is the higher-priority source — trust it over those docs.

### 2. Dead code left in `ChonkyMerge` from an even older "merge game" prototype
- **Problem:** `Assets/Scripts/ChonkyMerge/GameConfig.cs`, `AnimalSprites.cs`, and `SpriteFactory.cs` are not referenced by `PuzzleGame.cs` or `MainMenu.cs` (verified via grep — only self-references and each other). They describe a 14-tier animal system with per-tier colors/radii that predates even the Step/Roll/Hop mechanic.
- **Suspected cause:** Incomplete cleanup across two mechanic pivots (merge game → animals-as-mechanic → Bedtime Shuffle).
- **Files involved:** `Assets/Scripts/ChonkyMerge/GameConfig.cs`, `Assets/Scripts/ChonkyMerge/AnimalSprites.cs`, `Assets/Scripts/ChonkyMerge/SpriteFactory.cs`.
- **Recommended next step:** Confirm nothing references them (a fresh grep before deleting, in case a scene or a WIP branch does), then delete. Low risk, pure cleanup.

### 3. `ButtonId.HighScore` is a vestigial enum case
- **Problem:** `MenuButton.cs`'s `ButtonId` enum still has `HighScore`, and `MainMenu.cs` still has a `Panel.HighScore` case with UI for it, but **no button in the current menu ever triggers it** — the old high-score button slot was replaced by the "Levels" button. It's unreachable dead UI.
- **Suspected cause:** Same incremental-pivot leftover as #2.
- **Files involved:** `Assets/Scripts/ChonkyMerge/MenuButton.cs:5`, `Assets/Scripts/ChonkyMerge/MainMenu.cs:212`, `MainMenu.cs:259-265`.
- **Recommended next step:** Either wire a real "best/stats" entry point to it, or remove the enum case and its panel branch. Low priority, cosmetic.

### 4. `tools/process_animals.py` references a folder that no longer exists
- **Problem:** The script reads from `Assets/Resources/Art/raw/`, which is not present in the repo (raw art now lives only in the git-ignored `_ArtSource/`, per commit `0d34a77`). Running the script as-is will fail immediately.
- **Suspected cause:** Consolidation of raw-art convention into `_ArtSource/` happened without updating/removing this script.
- **Files involved:** `tools/process_animals.py`.
- **Recommended next step:** Either update its `RAW` path to point at the current `_ArtSource/` convention, or delete it if the `Art/animals` tier pipeline (see TODO #2) is being retired anyway.
- **Note:** requires `numpy`, `PIL`, and `scipy` (`scipy.ndimage`) — scipy was **not** confirmed installed in the dev environment as of the last check.

### 5. Uncommitted work at time of writing
- **Problem:** `Assets/Scripts/ChonkyMerge/MainMenu.cs` and `Assets/Scripts/SleepyZoo/PuzzleGame.cs` have unstaged modifications, and `Assets/Editor/StandaloneBuilder.cs` (+ its `.meta`) is untracked, relative to the last commit `a679ae9`.
- **Suspected cause:** Work in progress from the current/most recent session, not yet reviewed or committed.
- **Files involved:** the three listed above.
- **Recommended next step:** Before making further changes, run `git diff` on the two modified files to understand what's mid-flight, and decide with the user whether `StandaloneBuilder.cs` should be committed (it's explicitly marked "safe to delete" in its own header) or removed.

---

## Important Decisions

*(Do not reverse these without explicit product-owner sign-off — each was a deliberate pivot away from a rejected direction.)*

1. **Genre differentiation must be mechanical, not cosmetic.** A cute-animal skin on an existing genre was explicitly rejected multiple times. Rejected directions (in order): tilt-merge-in-a-jar, "Wobble Tower" physics stack, "Flow Free with animals" (per-animal movement rules). The current "Bedtime Shuffle" — one swipe moves every animal at once — was chosen *because* it's a genuinely different mechanic (closer to a "sliding block puzzle" / Rush Hour lineage than Flow Free).
2. **Deterministic, turn-based grid — no physics.** All movement is grid-snapped and computed, never simulated with Rigidbody/physics. This was a deliberate reliability choice (physics = flaky puzzles).
3. **Every level's 3-star target is the BFS-proven optimal move count**, never an arbitrary designer guess. Any new level must be validated with `python tools/gen_levels.py verifycs` (which parses the real `Levels` array and re-solves every level) before being considered done.
8. **Chapters break rules, they don't just add walls.** Progression is deliberately *not* "same rule, bigger board" (the Flow Free 4x4 → 5x5 model). Each 20-level chapter takes something the previous chapter taught as unchangeable and changes it, and the locked chapter's twist is **hidden in the UI on purpose**. Chapter 2's sticky beds specifically resolve chapter 1's most common frustration ("I had it on the bed and the next swipe pulled it off"), which is why chapter 1 must keep that frustration intact.
4. **IMGUI (`OnGUI`), not UGUI/UI Toolkit**, for all interface code. Consistent with the rest of the codebase; do not introduce a second UI system without discussing.
5. **No monetization, no accounts, no backend** at this stage. The game is local-only and free-standing.
6. **Fredoka (SemiBold) is the one and only font** — do not fall back to default Arial for new UI unless the font resource is missing (existing code already has that fallback pattern).
7. **Android package id is `com.wobblegames.wobblezoo`** — changing it after any Play Store upload would break update continuity. Treat as fixed.

---

## Terminology Glossary

Quick reference for names that appear in `PuzzleGame.cs` without much explanation:

| Term | Meaning |
|---|---|
| `Lv` | One level definition: width, height, `par` (optimal moves), hint text, wall cells, animal entities. |
| `EntDef` | One animal's start cell `(x,y)` and its target bed cell `(bx,by)`. Index in `ents[]` also indexes into `Pets[]`/`PetColors[]` (by `i % length`), so animal species/color is purely cosmetic, not gameplay-relevant. |
| `par` | The **true BFS-optimal** move count to solve the level (not a designer guess) — 3★ requires `moves <= par`, 2★ requires `moves <= par+2`, otherwise 1★. |
| `SlideSim` | Pure function: given all animal positions + one swipe direction, returns where every animal ends up after sliding to a stop. This is the entire mechanic — no other movement logic exists. |
| `SolveFrom` | BFS over `SlideSim` states from any position to the goal (all animals on their beds); returns the shortest swipe sequence. Powers both hints and the tutorial demo arrow. |
| `StateKey` | Packs all animal positions into one `long` for BFS visited-set hashing — assumes board dimensions small enough that `x*h+y` fits acceptably in the packing scheme; do not use on boards larger than roughly 8×8 with many animals without checking this still holds. |
| Checkpoint / gate | Every 4th level (see the `Gates` table / `RequiredStars`) stays locked until the player's **cumulative** star total reaches a threshold, even if the prior level alone is cleared. |
| Chapter | A 20-level block with its own rule set. `ChapterOf`, `ChapterFirstLevel`, `StickyBeds` etc. Chapter 2 starts at level index 20 and needs 36 stars. |
| Sticky beds | The chapter-2 rule: an animal that touches its own bed mid-slide stops there and is asleep for good. Two `_sticky` lines in `SlideSim`. |
| "Bedtime Shuffle" | The working/marketing name for the current one-swipe-moves-everyone mechanic, used in code comments and commit messages. Not necessarily the final shipped title — check `docs/PROJECT_MEMORY.md` for naming status. |

---

## Manual Smoke Test (no automated tests exist)

After any change to `PuzzleGame.cs` or `MainMenu.cs`, before considering work "done":

1. Run the batch-mode scene rebuild (Environment Requirements) and confirm the log ends with `"Cozy puzzle scene built and app configured."` and zero `error CS` lines.
2. Open the project in the Unity Editor, enter Play mode on the `MainMenu` scene.
3. Tap **Levels** → confirm the grid shows level 1 unlocked, others locked/greyed as expected, the star total line reads `0 / 120 stars` on a fresh `PlayerPrefs`, and the **Chapter 2 header reads `? ? ?`** with "Opens at 36 stars" (it must NOT leak the sticky-bed twist).
4. Tap into level 1 → confirm the tutorial arrow/demo appears before the first swipe.
5. Swipe once in the demoed direction → confirm all animals move together per `SlideSim`, Undo/Reset both work, and winning shows the win panel with correct star count and `moves`/`par` math.
6. Return to menu → confirm the star total updated and the next checkpointed level's lock/gate text matches `RequiredStars`.

---

## Environment Requirements

### Required environment variables
- **None.** No `.env` file, no API keys, no secrets are used anywhere in this project (verified by grep for `api_key|secret|password|token` across `Assets/Scripts` and `Assets/Editor` — no matches).

### Required tool versions
- **Unity Editor 6000.4.11f1** exactly (see `ProjectSettings/ProjectVersion.txt`). Install via Unity Hub; must include the **Android Build Support** module (+ SDK/NDK/OpenJDK) for APK builds.
- **Python 3.x** (stdlib only) for `tools/gen_levels.py` — the level generator/verifier. Not needed to build or run the game, but required to prove any level change is sound. `tools/process_animals.py` (currently broken per TODO #4) additionally needs `numpy`, `PIL` (Pillow), `scipy`.
- No Node.js, no npm — this is not a JS project.

### Commands
All Unity commands below assume Windows and the default Hub install path; adjust `Unity.exe` path per-OS/per-user.

**Rebuild the Puzzle scene + app config from code** (safe, idempotent — the scene is entirely code-built):
```bash
"/c/Program Files/Unity/Hub/Editor/6000.4.11f1/Editor/Unity.exe" -batchmode -quit -nographics -projectPath "<repo-root>" -logFile "<logfile-path>" -executeMethod ChonkyMerge.EditorTools.PuzzleSceneBuilder.Build
```

**Build the Android APK** (output: `Builds/WobbleZoo.apk`):
```bash
"/c/Program Files/Unity/Hub/Editor/6000.4.11f1/Editor/Unity.exe" -batchmode -quit -projectPath "<repo-root>" -executeMethod ChonkyMerge.EditorTools.ApkBuilder.BuildAndroid -logFile "<logfile-path>"
```
Check the log for `"APK build result: Succeeded"`. Exits with code 1 on failure when run in batch mode.

**Build a Windows standalone** (local testing only, temporary tool):
```bash
"/c/Program Files/Unity/Hub/Editor/6000.4.11f1/Editor/Unity.exe" -batchmode -quit -projectPath "<repo-root>" -executeMethod ChonkyMerge.EditorTools.StandaloneBuilder.BuildWin -logFile "<logfile-path>"
```

**"Test"** — there is no automated test suite. Verification = a clean batch-mode compile/build (0 `error CS` lines in the log) plus manual play in the Unity Editor or on-device.

**Deploy** — no CI/CD or store-deploy pipeline exists. APK must be manually uploaded to Play Console when ready (never done yet — no store listing exists).

**Important:** Unity batch-mode builds **fail if the Unity Editor GUI is already open** on the same project. Having Unity **Hub** open (not the Editor) is fine.

---

## File Map

| File | One-line description |
|---|---|
| `Assets/Scripts/SleepyZoo/PuzzleGame.cs` | The entire puzzle game: levels, slide-simulation mechanic, BFS hint solver, star economy, all runtime-generated visuals and IMGUI. |
| `Assets/Scripts/ChonkyMerge/MainMenu.cs` | Landing page: background/floaters/logo/buttons, Settings panel, and the Levels picker panel with per-level stars. |
| `Assets/Scripts/ChonkyMerge/MenuButton.cs` | Tappable sprite-button component with squash/bounce feedback; defines `ButtonId` enum. |
| `Assets/Scripts/ChonkyMerge/Sfx.cs` | Procedural audio (click/pop blips generated in code); reads/writes the `sound_on` PlayerPrefs flag. |
| `Assets/Scripts/ChonkyMerge/NativeShare.cs` | Android native share-sheet intent wrapper; no-ops to `Debug.Log` in the editor. |
| `Assets/Scripts/ChonkyMerge/GameConfig.cs` | **Dead code** — unused 14-tier animal config from a retired mechanic. See TODO #2. |
| `Assets/Scripts/ChonkyMerge/AnimalSprites.cs` | **Dead code** — unused sprite loader for the retired tier system. See TODO #2. |
| `Assets/Scripts/ChonkyMerge/SpriteFactory.cs` | **Dead code** — unused procedural-circle sprite generator from the original merge-game prototype. See TODO #2. |
| `Assets/Editor/PuzzleSceneBuilder.cs` | Editor script: rebuilds `Puzzle.unity` from code, sets build settings/scenes, product name, bundle id, icon. |
| `Assets/Editor/ApkBuilder.cs` | Editor script: builds the Android APK to `Builds/WobbleZoo.apk` (IL2CPP, ARM64, min SDK 24). |
| `Assets/Editor/StandaloneBuilder.cs` | **Untracked, temporary** — Windows build for local screenshotting. Marked "safe to delete" in its own header. |
| `Assets/Editor/ArtImportSettings.cs` | `AssetPostprocessor` that auto-configures every PNG under `Resources/Art/` as an uncompressed Sprite. |
| `Assets/Scenes/MainMenu.unity` / `Puzzle.unity` | Minimal scene files — each holds one empty GameObject with the relevant script attached; everything else is code-built at runtime. |
| `Assets/Resources/Art/pets/` | The 10 animal sprites actually used by the current mechanic (`dog, rabbit, panda, owl, pig, frog, penguin, bear, duck, cow`). |
| `Assets/Resources/Art/animals/` | **Legacy** tier1–tier14 animal sprites from the retired mechanic; no longer loaded by any live code path. |
| `Assets/Resources/Fonts/Fredoka.ttf` | The game's sole font (Fredoka SemiBold). |
| `docs/ASSETS_AND_TODO.md` | **Stale** — describes the retired per-animal-ability mechanic. See TODO #1. |
| `docs/ART_PROMPTS.md` | **Stale** — art-generation prompts tied to the retired mechanic/level-select map plan. |
| `tools/gen_levels.py` | **Live and important** — generates and BFS-verifies levels for both chapter rule sets. `verifycs` re-proves every par in `PuzzleGame.cs`. |
| `tools/process_animals.py` | **Broken/historical** — art-processing script pointing at a folder that no longer exists. See TODO #4. |
| `ProjectSettings/ProjectSettings.asset` | Unity project settings — product name "Wobble Zoo", bundle version, Android SDK settings live here. |
| `Packages/manifest.json` | UPM dependency manifest — stock Unity modules only, no third-party packages. |

---

## Known Gotchas

- **Two full mechanic pivots have happened** in this repo's history: (1) an original "merge game" prototype → (2) "animals-as-mechanic" (each animal has one of Step/Roll/Hop/Push/Sleep/Fly) → (3) current "Bedtime Shuffle" (one swipe slides everyone). Dead code and stale docs from stages 1 and 2 still exist (see TODOs #1–#2). **Always check whether a file is actually referenced by `PuzzleGame.cs` or `MainMenu.cs` before trusting its doc comments** — several files' comments describe behavior the code no longer has.
- **Both scenes are code-built, not hand-wired.** If a scene file (`.unity`) ever looks "broken" or empty in the Editor, that's expected — re-run `PuzzleSceneBuilder.Build` rather than trying to manually wire GameObjects.
- **Levels are a hardcoded C# array**, not data files (`Levels` in `PuzzleGame.cs:54`). There is no external level-editor or JSON format. Adding a level means editing this array directly and hand-verifying solvability (ideally with a BFS script mirroring `SlideSim`, since the in-game hint solver's `SolveFrom` already provides that logic as reference).
- **`_ArtSource/` is git-ignored.** A fresh clone will not have raw art, reference images, or "pending" unused art. Do not assume it exists; do not add gameplay-critical assets there.
- **Unity batch-mode builds fail if the Unity Editor is already open** on this project (Hub alone is fine). If a build command mysteriously fails, check for a stray open Editor instance first.
- **`ArtImportSettings.cs` force-sets all `Resources/Art/*` textures to Uncompressed.** This keeps sprite quality high but means large PNGs bloat the build significantly — keep background images downscaled (this was a real issue addressed in an earlier commit, per `docs/ASSETS_AND_TODO.md`'s now-outdated background list).
- **PlayerPrefs keys in active use:** `zoo_level` (last/selected level index), `zoo_stars_<i>` (best stars per level index), `sound_on` (0/1), `chonky_best` (leftover from the old merge game — still read by the vestigial `HighScore` panel, TODO #3). Changing any of these keys' names will silently reset player progress.
- **No git remote issues** — `origin` is configured (`https://github.com/Shabbir-Nuruddin/wobblezoo.git`) and `master` tracks `origin/master`. (An earlier note in project memory claiming "no remote configured" is stale — a remote exists now.)

---

## Git Workflow

- **Current branch:** `master`, tracking `origin/master`.
- **Uncommitted changes at time of writing:**
  - Modified (unstaged): `Assets/Scripts/ChonkyMerge/MainMenu.cs`, `Assets/Scripts/SleepyZoo/PuzzleGame.cs`
  - Untracked: `Assets/Editor/StandaloneBuilder.cs`, `Assets/Editor/StandaloneBuilder.cs.meta`
- **No commits are pending push** — `master` is even with `origin/master` as of the last fetch; only working-tree changes exist locally.
- Before committing, always verify a clean batch-mode build first (see Environment Requirements → Rebuild the Puzzle scene) — this project's own convention (per commit history) is "never claim a change is done without a build that actually passed."

---

## AI Resume Instructions

1. **Read this file (`AI_CONTEXT.md`) first, in full**, before reading anything else — it supersedes `docs/ASSETS_AND_TODO.md` and `docs/ART_PROMPTS.md`, which are stale (see Known Gotchas and TODO #1).
2. **Then inspect, in this order:**
   - `Assets/Scripts/SleepyZoo/PuzzleGame.cs` — the entire game lives here; read `Levels`, `SlideSim`, `DoMove`, `SolveFrom`, `CheckWin`.
   - `Assets/Scripts/ChonkyMerge/MainMenu.cs` — landing page + level picker.
   - `git status` and `git diff` — to see exactly what's mid-flight beyond what's summarized above (this file may be slightly out of date by the time you read it; git is ground truth for uncommitted state).
   - `docs/PROJECT_MEMORY.md` — for the durable product vision, target users, and naming/roadmap context that doesn't change week to week.
3. **Then continue from the highest-priority TODO** in the Active Problems / TODOs section above — currently, that is **TODO #5 (review and either commit or discard the in-flight uncommitted changes)**, since building on top of unreviewed mid-flight edits risks compounding confusion. After that, TODOs are ordered roughly by how much they mislead a future reader (stale docs first, then dead code, then cosmetic/low-risk items).
4. **Before declaring any task done, run a batch-mode build** (see Environment Requirements) and confirm zero `error CS` lines in the log — this project's working convention treats "build passed" as the bar for "done," not just "compiles in my head."
5. **Update this file** as part of any nontrivial change — especially the "Current Working State," "Active Problems," and "Git Workflow" sections — so the next agent (human-directed or otherwise) doesn't have to re-derive what you just learned.
