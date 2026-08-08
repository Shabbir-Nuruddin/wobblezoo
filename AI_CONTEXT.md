# AI_CONTEXT.md

> **Audience:** AI coding agents (Claude, GPT, Gemini, etc.), not humans.
> Read this file fully before touching code. It changes frequently as work
> progresses — treat it as the current source of truth, more current than any
> commit message or docs/ file. If this file and `docs/ASSETS_AND_TODO.md`
> disagree, **trust this file** (see Known Gotchas).

---

## Project Identity

- **Name:** Tuck In (renamed from "Wobble Zoo" in the design pass — the old name described a physics game two pivots ago). The Android applicationId is deliberately **still** `com.wobblegames.wobblezoo`: it is the app's permanent identity on Play and changing it would orphan every existing install. Company "Wobble Games".
- **Purpose:** A cozy, deterministic mobile puzzle game for the Google Play Store — swipe to slide sleepy animals into their beds.
- **Status:** Prototype / pre-release. No store listing yet. Core loop, **130 levels across 8 chapters**, menu, blanket-path level select and the zoo are built and building cleanly. Never shipped to Play Store.

---

## Architecture Snapshot

- **Engine/framework:** Unity **6000.4.11f1** (Unity 6 LTS-track editor). C# scripting, IMGUI (`OnGUI`) for all UI — no UGUI Canvas, no UI Toolkit.
- **Runtime target:** Android (min SDK 24 / Android 7.0), IL2CPP, ARM64. A temporary Windows Standalone build path also exists for local screenshotting.
- **Package manager:** Unity Package Manager (`Packages/manifest.json`) — stock Unity modules only, no third-party packages, no npm/pip involved in the game itself.
- **Important directories:**
  - `Assets/Scripts/SleepyZoo/` — the puzzle game itself. **Only one file: `PuzzleGame.cs`** (~845 lines). This is the core gameplay.
  - `Assets/Scripts/UI/` — **the design system.** `Ui.cs` (virtual 390x844 canvas, palette, fonts, primitives, widgets) and `Icons.cs` (every icon rasterised in code from 24-unit SVG coordinates). **Every screen outside the board goes through these two files.** Do not hand-roll a colour, a font size or a rounded rect anywhere else.
  - `Assets/Scripts/ChonkyMerge/` — everything else runtime: `MainMenu.cs` (home, map, dorm, decorate, settings), `Dorm.cs` (snacks/moods/asleep), `Zoo.cs`, `Sfx.cs`, `NativeShare.cs` (Android share sheet). Also contains **dead code** — see Known Gotchas.
  - `Assets/Editor/` — editor-only build tooling, run via `-executeMethod` in batch mode (see Environment Requirements): `PuzzleSceneBuilder.cs`, `ApkBuilder.cs`, `StandaloneBuilder.cs`, `ArtImportSettings.cs`.
  - `Assets/Scenes/` — `MainMenu.unity` and `Puzzle.unity`. Both scenes are built entirely from code at `Start()` — there is no manual GameObject wiring to preserve; the scenes just hold one empty GameObject with the relevant MonoBehaviour attached.
  - `Assets/Resources/Art/` — all sprites, loaded at runtime via `Resources.Load`. Subfolder `Art/pets/` holds the 10 sprites the current puzzle uses (`dog, rabbit, panda, owl, pig, frog, penguin, bear, duck, cow`). Subfolder `Art/animals/` (`tier1..tier14`) is **leftover from a retired mechanic** — see Known Gotchas.
  - `Assets/Resources/Fonts/Fredoka.ttf` — the game's only font.
  - `_ArtSource/` — git-ignored. Raw/original art, copyrighted reference images (never shipped), and pending (not-yet-used) generated art. Do not expect this folder to exist on a fresh clone.
  - `docs/` — two Markdown handoff docs, both **stale** (predate the current mechanic). See Known Gotchas.
  - `tools/gen_levels.py` — **the level generator and verifier.** Mirrors `SlideSim` for both chapter rule sets; `verifycs` re-proves every par in `PuzzleGame.cs`. Run it after touching the `Levels` array.
  - `tools/process_animals.py` — a Python art-processing script for the retired `Art/raw` → `Art/animals` pipeline. Its input folder (`Assets/Resources/Art/raw/`) no longer exists in the repo. Effectively dead/historical.
  - `Builds/` — git-ignored build output (`TuckIn.apk`, `Win/`).
- **Database/storage:** None. All persistence is `PlayerPrefs` (local device key-value store) — see keys list below.
- **External services/APIs:** None. No backend, no analytics SDK wired in, no ads SDK, no IAP. `NativeShare.cs` calls the OS share sheet only (no network call).

---

## Current Working State

### What was being worked on most recently (per git log, newest first)
**Phase 5 — Play Store readiness.** The game was content-complete but not shippable: all eighteen Android icon slots were **empty** (Play would have got the default Unity logo), the target API was set to "automatic" (meaning "whatever SDK this machine happens to have"), and there was no App Bundle path at all — Play has not accepted plain APKs for new apps since 2021. Added `IconSetup` (generates and assigns adaptive/round/legacy icons from art already in the repo), `StoreBuilder` (signed `.aab`, target API 36), the whole `store/` kit, and extended the screenshot tour into the puzzle scene so the listing can show real gameplay.

Before that, Phases 3 and 4. Phase 3 took the game from 40 levels to **130** — six new chapters, each with one new toy, all BFS-verified by both the Python generator and the in-engine C# audit. Phase 4 added **Tonight's Puzzle** (a daily level with its own pool in `DailyLevels.cs`), a **streak that cools instead of resetting**, lanterns as the streak reward, and **`SaveGuard`**, a local mirror of every save key that restores itself if PlayerPrefs is ever lost.

### What is already completed
- **Core mechanic ("Bedtime Shuffle"):** one swipe direction slides **every** animal at once, each one sliding until it hits a wall/edge/another animal (see `SlideSim` in `Assets/Scripts/SleepyZoo/PuzzleGame.cs:436`). This replaced an earlier "each animal has its own movement rule" mechanic entirely — that old system (Step/Roll/Hop/Push/Sleep/Fly per animal, tier-based `GameConfig`) is gone from gameplay.
- **130 levels across eight chapters** (20 + 20 + 15×6), each BFS-verified with `par` = true optimal move count (`Levels` array in `PuzzleGame.cs`). **Par is capped at 9, and chapter one never exceeds 3 animals or par 7.** This was re-tuned after play-testing on a phone: the previous curve reached 4 animals at par 9 by level 15 and 5 animals at par 12 by level 20, which put the hardest content in the game inside the chapter that is supposed to be teaching the rule. For a game people open in bed that reads as work, not challenge. The curve lives in `CH1_*` / `CH2_*` / `RAMP_*` in `tools/gen_levels.py` — edit those and regenerate, never hand-edit the C#. **Par is capped by design** — a level may be hard to *think* about but must never be long to *play*, because a player needing 12 optimal swipes really spends 20-25 exploring and undoing. Difficulty comes from board shape, blocks and animal count, never from a longer solution. The animal count ramps in **plateaus** (1,1,1, 2,2,2,2, 3,3,3,3,3, 4,4,4,4,4, 5,5,5) so each new friend gets easy levels before the thinking gets harder, and **chapter 2 restarts the whole ramp** at 2 animals / par 3 because sticky beds are a new game. The full ramp is the `CHAPTER1` / `CHAPTER2` tables in `tools/gen_levels.py` — edit those and re-run `plan`, don't hand-edit the C# array. **Chapter 2 changes the rule**: beds become *sticky* — an animal that touches its own bed, even mid-slide, stops there and never moves again (it becomes a wall for everyone else). This is two `_sticky`-guarded lines inside `SlideSim`; the solver, hints and stars all inherit it automatically. Every chapter-2 level is *unsolvable* under chapter-1 rules (enforced by the generator), so the twist is load-bearing, not decorative.
- **Eight chapters, each with one new toy** (`Rules` in `PuzzleGame.cs`, mirrored by `TOY` in `gen_levels.py`): 1 plain, 2 sticky beds, 3 any-animal-any-bed, 4 slippery rugs (cross but never stop), 5 honey (touch it and stop dead), 6 burrow pairs (in one, out the other, still sliding), 7 one heavy animal that only moves when pushed, 8 everything at once. **Sticky beds stay on from chapter 2**; every later chapter adds a *visible object* rather than an invisible rule, so nothing compounds in the player's head. All of it lives in one function — `PuzzleGame.Walk` — so the game, the hint solver and the generator can't disagree.
- **Chapters are not all the same length**: `ChapterStart = {0,20,40,55,70,85,100,115}` — the first two are 20 levels, the rest 15. Never assume `level / ChapterSize`.
- **Two independent verifications.** `python tools/gen_levels.py audit` re-solves every level with the Python rules; **`-executeMethod ChonkyMerge.EditorTools.LevelAudit.Run`** re-solves every level with the *game's own* C# SlideSim/BFS and exits 1 on any mismatch. The second one is the only check that catches the two implementations drifting apart — **run it after any rule change**.
- **`tools/gen_levels.py`** — the level generator/verifier. It mirrors `SlideSim` exactly for both rule sets, derives `par` from BFS, rejects levels where the optimal solution uses too few distinct directions, and measures a "dead fraction" (share of reachable states from which the goal is no longer reachable) to keep levels forgiving. `python tools/gen_levels.py plan` regenerates the whole 40-level ramp and rewrites the `Levels` array in place; `python tools/gen_levels.py verify` re-proves the Python sim matches the C# one against a frozen fixture of older levels — **run it before trusting any generated level**.
- **In-game optimal-move hint solver** (`SolveFrom`, `PuzzleGame.cs:486`) — reused for both the level-1 tutorial demo arrow and the in-play "Need a hint?" button. Same BFS also used for design-time level validation.
- **Star economy with checkpoints**: 1–3 stars per level (3★ = `par`, 2★ = `TwoStarMoves(par)` = par + half again, minimum +3 — deliberately generous now that pars are short); a running total gates every 4th level behind a cumulative star threshold (`Gates` / `RequiredStars` / `IsUnlocked`). The big one is **level 21 = 36 of a possible 60 stars**, which is the door into chapter 2. 390 stars total across 130 levels. Gates are **built, not typed** (`BuildGates`): one every 4 levels at 1.55× the level index, a heavier one on each chapter door at 1.8×, and a running-max pass so a later checkpoint can never ask for less than an earlier one.
- **Chapter reveal as the retention hook**: the level picker shows a locked chapter as `Chapter 2 — ? ? ?` with only a tease ("One bedtime rule you know by heart is about to change") and the star cost. Clearing level 20 shows a "Chapter complete!" panel whose button reads **See what changed**. Level 21 gets its own guided-arrow tutorial (`TaughtKey(chapter)`), same as level 1.
- **The full visual redesign** (`design/wobble-zoo-redesign/`, implemented across `Assets/Scripts/UI/` + `MainMenu.cs` + `PuzzleGame.cs`). Everything is laid out on a **virtual 390x844 canvas** (`Ui.Frame`/`Ui.R`) mapped onto the safe area, which is what stopped text colliding with the logo on narrow phones. The system rules: **one primary per screen** (chunky terracotta with a pressable bottom edge — nothing else gets it), secondary = outline pill, tertiary = round icon on a ghost disc; cream is day and deep umber is night; **Caprasimo** for numbers/names/titles and **Figtree** for reading; **stars are gold (progress), snacks are amber (affection), and they never mix.**
- **Home** is one door: a terracotta *Continue* that names the level and room you are heading into, a dorm shelf showing who actually lives with you, and a permanent three-item bottom rail (Map / Dorm / Tonight).
- **The map** replaced the blanket path: each chapter is a full-width coloured **band** in its own room's palette, with a winding dotted path of level nodes through it; the level you are up to is a bigger terracotta node with a breathing halo and a "4 animals · par 6" tag. Locked chapters go dark and keep the `? ? ?` tease. (The older description below is kept because the node/stitch maths is unchanged.) Previously: each chapter is a winding, dotted-stitch path of pillow-shaped level nodes (`DrawPillow` / `DrawStitches` / `PathX`). The level the player is up to gets a soft gold halo; unreached stitching is faded; the locked-chapter `? ? ?` tease is unchanged. A star-gated node is tappable and explains itself (`DrawGateHelp`) with a one-tap replay of `PuzzleGame.EasiestTopUpLevel()`.
- **The dorm** (was "the zoo") is now a room you visit rather than a list you scroll: friends sit in their own beds around a lamplit room, and tapping one opens a sheet with **Feed** (1 snack), **Pet** (always free) and **Tuck in**. `Dorm.cs` owns snacks/moods/asleep. Two rules it must keep: **nothing decays** (a fed animal never gets hungry again on a timer — this is a bedtime game), and **snacks are not progress** (they gate nothing, so skipping the dorm entirely costs the player nothing). `Decorate` recolours the dorm's lamplight — it is the one screen the design named but never drew, so it was kept deliberately small and built only from colours the game already owns.
- **The zoo's schedule** (`Assets/Scripts/ChonkyMerge/Zoo.cs`): 10 animals that move in on a schedule the player can see — star totals and chapter completions, never randomness. Each has a signature colour matching its board colour, a tier (Friend/Special/Guest) that only changes how it *looks*, and a settling stage (Visiting -> Snuggled -> Dreaming) derived from stars earned since it arrived. **The zoo owns no save data** — every answer is derived from `zoo_stars_*`; the sole exception is `zoo_seen` (how many arrivals have been announced), which drives the one-time "Someone moved in!" card. The win panel shows `Zoo.NextLine()` so stars visibly buy something.
- **A room per chapter**: `Rooms` in `PuzzleGame.cs` holds eight painted skies (nursery, treehouse, meadow, snow cabin, pantry, garden, library, under the stars) — sky gradient, moon position/size, three hill silhouettes, and a twinkle colour/density that doubles as stars, fireflies or snow. `BgGradient(chapter)` paints and caches one per chapter, and the camera clear colour follows. The photo-real `Art/bg_*.png` images are from an older art direction and stay unused on purpose (they clash with the flat pastel style).
- **Feel pass**: animals squash on landing and breathe while idle, a thump plays pitched by how far each one skidded (max two per swipe), sleeping animals get a soft puff of motes, and the win panel rings its stars one at a time. Short Android vibrations via `Haptics.cs` (own `haptics_on` setting, toggled in Settings; no-op off-device).
- **Fully procedural visuals for the puzzle screen** — board tiles, bed glow rings, arrow, button pill fallback, background gradient/moon are all generated in code (`RoundedTile`, `SoftDisc`, `ArrowSprite`, `BgGradient`, `MakeButtonTex` in `PuzzleGame.cs`), so the puzzle scene has minimal art dependencies.
- **Tonight's Puzzle** (`Assets/Scripts/ChonkyMerge/Nightly.cs` + `DailyLevels.cs` + `LoadDaily`/`DrawDailyWin`): one extra level a night, picked from a generated pool by date, so every player gets the same board with nothing fetched or agreed on. Four rules make it safe: it **pays no stars** (stars gate chapters — a daily that paid them would drag players past the levels that teach the rules and punish anyone who skips it); it uses **chapter one's rules only** (a brand-new player might tap it first, and it must never spoil a twist); the night **rolls over at 4am**, not midnight; and a **missed night costs one night**, not the streak. Nights light 8 lanterns in the zoo — a reward that can't unbalance anything because it isn't a currency.
- **`SaveGuard`** mirrors every save key to a JSON file in `persistentDataPath` and restores it automatically if PlayerPrefs comes up empty. Deliberately all-or-nothing: it restores only when there is *no* progress at all, because a partial restore is a guess and could resurrect progress someone meant to erase. Not cloud save — it survives a lost prefs file, not an uninstall.
- **Android APK build path** verified working headlessly (see Environment Requirements).
- **Play Store readiness** (`Assets/Editor/IconSetup.cs`, `Assets/Editor/StoreBuilder.cs`, `store/`): app icons for every Android slot including adaptive (foreground drawn inside the 72/108 safe zone, because launchers mask the icon and will crop a third of it); a signed App Bundle path targeting API 36; and a listing kit — description, privacy policy, content-rating and data-safety answers, 512 icon, 1024×500 feature graphic, and 16 screenshots at 1216×2160. **Signing credentials are read from environment variables and scrubbed from `PlayerSettings` on editor exit** — no password ever reaches `ProjectSettings.asset`, and `.gitignore` blocks `*.keystore`/`*.jks`/`*.p12`. The whole publishing sequence is written out for a non-coder in `store/RELEASE_CHECKLIST.md`.

### What is partially implemented
- **Windows Standalone build** (`Assets/Editor/StandaloneBuilder.cs`) is a dev tool for *looking* at the IMGUI screens, not a shipping target. Run the built exe with `-shots <folder> -shotstars <n>` and `MainMenu.ShotTour` walks home / blanket path / zoo / settings, saves a PNG of each and hard-kills the process (so the faked star totals are never flushed to PlayerPrefs). This is the only way to check a screen actually looks right — use it after any UI change.
- **Audio**: wired in. Seven CC0 Kenney clips live in `Assets/Resources/Audio/` (tap, land, sleep, star, win, undo, locked) and `Sfx.cs` is a small event bank on top of them; the swipe whoosh is still generated in code. See `docs/CREDITS.md` for the mapping and licences. **Background music is still an open slot** — the four MP3s in `_ArtSource/audio_pending/` look like commercial documentary tracks with no licence file and must not ship.
- **Sharing copy** is hardcoded and generic (`MainMenu.cs:215`) — not tested on a real device.

### What is intentionally not implemented
- No level-select "map" screen (winding path with node markers) — an earlier plan (see `docs/ASSETS_AND_TODO.md`) was superseded by the simpler grid-panel `Levels` button in the current menu. Do not resurrect the map plan without checking with the product owner first.
- No monetization (ads/IAP), no analytics, no accounts, and **no cloud save** — `SaveGuard` is a local mirror, not a server. Adding real cloud save means adding an account system, which the product deliberately doesn't have.
- **No streak punishment, ever.** A missed night costs one night, never the whole streak, and nothing in the game expires, decays into a worse state, or asks the player to come back at a particular time. This is a bedtime game; anything that turns it into an obligation is off the table.
- **The nightly puzzle pays no stars and never uses a later chapter's rules.** Both constraints are load-bearing (see `Nightly.cs`) — changing either breaks the campaign's pacing or spoils a twist.
- No automated tests (no test framework is set up in this Unity project at all).

---

## Two traps this codebase has already sprung

1. **IMGUI always draws on top of the camera.** The puzzle screen's sky, moon and hills are WORLD-space sprites behind the board (`SpawnBackground`/`BgGradient`). Painting a full-screen gradient in `OnGUI` hides the board completely. Only the win panel is allowed to cover the screen.
2. **Texture row 0 is the BOTTOM.** `Icons.Raster` and `Ui.StarTex` both take y-down SVG coordinates, so both flip y explicitly. Forget it and every asymmetric icon (bed, lock, drop, arrows) and the star ship upside down — which is subtle enough to survive a casual look.

## Active Problems / TODOs

### 0. Burrow pairs are told apart by colour alone (accessibility)
- **Problem:** Chapter 6 pairs burrows using `HolePair` colours only. Roughly 1 in 12 men has some red/green colour blindness, and a player who can't separate the pairs can't reason about where a burrow leads — the chapter becomes guesswork rather than a puzzle.
- **Files involved:** `HolePair` and the burrow drawing in `Assets/Scripts/SleepyZoo/PuzzleGame.cs`.
- **Recommended next step:** Add a non-colour marker per pair — one/two/three dots, or a small shape — so the pairing survives without colour. Not a launch blocker, and it is called out honestly in `store/RELEASE_CHECKLIST.md` under "things deliberately not done", but it should be fixed before the game is pushed hard.

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
- **RESOLVED.** `MenuButton.cs` was deleted in the design pass: the new `MainMenu.cs` draws every control through `Ui`, so the sprite-button component (and its vestigial `HighScore` enum case) had no callers left.
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
| `par` | The **true BFS-optimal** move count to solve the level (not a designer guess) — 3★ requires `moves <= par`, 2★ requires `moves <= TwoStarMoves(par)` (par + half again, min +3), otherwise 1★. Pars run 2-12 only. |
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
3. Tap **Levels** → confirm the blanket path shows level 1 unlocked, others locked/greyed as expected, the star total line reads `0 / 390 stars` on a fresh `PlayerPrefs`, and the **Chapter 2 header reads `? ? ?`** with "Opens at 36 stars" (it must NOT leak the sticky-bed twist).
4. Tap into level 1 → confirm the tutorial arrow/demo appears before the first swipe.
5. Swipe once in the demoed direction → confirm all animals move together per `SlideSim`, Undo/Reset both work, and winning shows the win panel with correct star count and `moves`/`par` math.
6. Return to menu → confirm the star total updated and the next checkpointed level's lock/gate text matches `RequiredStars`.
7. Tap **Tonight** (top right, under the sound/share icons) → confirm it loads a puzzle titled "Tonight's Puzzle", that winning it awards **no stars** (the menu star total must be unchanged), and that the win panel counts nights rather than offering a next level.
8. **Any UI change must be looked at, not reasoned about.** Build the Windows player and run the screenshot tour — it is the only way to catch overlap and clipping, and it has caught real bugs every single time (the arrival card's button printed straight through its own text; the Tonight pill printed over the speaker icon):
   ```
   Unity.exe -batchmode -quit -projectPath . -executeMethod ChonkyMerge.EditorTools.StandaloneBuilder.BuildWin
   Builds/Win/TuckIn.exe -screen-width 900 -screen-height 1600 -screen-fullscreen 0 -shots <folder> -shotstars 60
   ```
   Note the method is `BuildWin`, not `BuildWindows`. The tour fakes progress in memory and hard-kills the process so a real save is untouched — but **Unity can still flush PlayerPrefs behind your back**, so the tour explicitly resets `zoo_seen` rather than assuming it starts clean.

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

**Build the Android APK** (output: `Builds/TuckIn.apk`):
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
| `Assets/Scripts/ChonkyMerge/MainMenu.cs` | Home, the chapter-band map, the interactive dorm, Decorate, Settings and the arrival card. All drawn through `Ui`. |
| `Assets/Scripts/UI/Ui.cs` | The design system: virtual 390x844 canvas, palette, Caprasimo/Figtree, rounded-rect/gradient/star primitives, and the Primary/Outline/GhostDisc/Chip/Bar widgets. |
| `Assets/Scripts/UI/Icons.cs` | Every UI icon, rasterised at startup from 24-unit SVG coordinates. All white — tint at draw time. |
| `Assets/Scripts/ChonkyMerge/Dorm.cs` | Snacks, moods and who is asleep; the feed/pet/tuck actions and the lamplight theme. |
| `Assets/Scripts/ChonkyMerge/Sfx.cs` | The game's sound bank: seven CC0 clips from `Resources/Audio` + a code-generated swipe whoosh; reads/writes `sound_on`. |
| `Assets/Scripts/ChonkyMerge/Haptics.cs` | Short Android vibrations (8-26ms) for landing/sleeping/winning; own `haptics_on` flag; no-op in editor and off-device. |
| `Assets/Resources/Audio/` | The seven shipped sound effects (Kenney, CC0 — see `docs/CREDITS.md`). |
| `docs/CREDITS.md` | Asset licences and the sound-event mapping; also records which assets are deliberately NOT shipped. |
| `Assets/Scripts/ChonkyMerge/NativeShare.cs` | Android native share-sheet intent wrapper; no-ops to `Debug.Log` in the editor. |
| `Assets/Scripts/ChonkyMerge/GameConfig.cs` | **Dead code** — unused 14-tier animal config from a retired mechanic. See TODO #2. |
| `Assets/Scripts/ChonkyMerge/AnimalSprites.cs` | **Dead code** — unused sprite loader for the retired tier system. See TODO #2. |
| `Assets/Scripts/ChonkyMerge/SpriteFactory.cs` | **Dead code** — unused procedural-circle sprite generator from the original merge-game prototype. See TODO #2. |
| `Assets/Editor/PuzzleSceneBuilder.cs` | Editor script: rebuilds `Puzzle.unity` from code, sets build settings/scenes, product name, bundle id, icon. |
| `Assets/Editor/ApkBuilder.cs` | Editor script: builds the Android APK to `Builds/TuckIn.apk` (IL2CPP, ARM64, min SDK 24). |
| `Assets/Editor/StandaloneBuilder.cs` | Windows build, for *looking* at the IMGUI screens. Run the exe with `-shots <folder> -shotstars <n>` for an automatic screenshot tour (`MainMenu.ShotTour`); faked stars are never written to disk. |
| `Assets/Scripts/ChonkyMerge/Zoo.cs` | The animal roster, arrival rules, settling stages and the arrival-card bookkeeping. Derived from stars; owns only `zoo_seen`. |
| `Assets/Editor/ArtImportSettings.cs` | `AssetPostprocessor` that auto-configures every PNG under `Resources/Art/` as an uncompressed Sprite. |
| `Assets/Scenes/MainMenu.unity` / `Puzzle.unity` | Minimal scene files — each holds one empty GameObject with the relevant script attached; everything else is code-built at runtime. |
| `Assets/Resources/Art/pets/` | The 10 animal sprites actually used by the current mechanic (`dog, rabbit, panda, owl, pig, frog, penguin, bear, duck, cow`). |
| `Assets/Resources/Art/animals/` | **Legacy** tier1–tier14 animal sprites from the retired mechanic; no longer loaded by any live code path. |
| `Assets/Resources/Fonts/Fredoka.ttf` | The game's sole font (Fredoka SemiBold). |
| `docs/ASSETS_AND_TODO.md` | **Stale** — describes the retired per-animal-ability mechanic. See TODO #1. |
| `docs/ART_PROMPTS.md` | **Stale** — art-generation prompts tied to the retired mechanic/level-select map plan. |
| `tools/gen_levels.py` | **Live and important** — generates and BFS-verifies levels for both chapter rule sets. `verifycs` re-proves every par in `PuzzleGame.cs`. |
| `tools/process_animals.py` | **Broken/historical** — art-processing script pointing at a folder that no longer exists. See TODO #4. |
| `ProjectSettings/ProjectSettings.asset` | Unity project settings — product name "Tuck In", bundle version, Android SDK settings live here. |
| `Packages/manifest.json` | UPM dependency manifest — stock Unity modules only, no third-party packages. |

---

## Known Gotchas

- **Two full mechanic pivots have happened** in this repo's history: (1) an original "merge game" prototype → (2) "animals-as-mechanic" (each animal has one of Step/Roll/Hop/Push/Sleep/Fly) → (3) current "Bedtime Shuffle" (one swipe slides everyone). Dead code and stale docs from stages 1 and 2 still exist (see TODOs #1–#2). **Always check whether a file is actually referenced by `PuzzleGame.cs` or `MainMenu.cs` before trusting its doc comments** — several files' comments describe behavior the code no longer has.
- **Both scenes are code-built, not hand-wired.** If a scene file (`.unity`) ever looks "broken" or empty in the Editor, that's expected — re-run `PuzzleSceneBuilder.Build` rather than trying to manually wire GameObjects.
- **Levels are a hardcoded C# array**, not data files (`Levels` in `PuzzleGame.cs:54`). There is no external level-editor or JSON format. Adding a level means editing this array directly and hand-verifying solvability (ideally with a BFS script mirroring `SlideSim`, since the in-game hint solver's `SolveFrom` already provides that logic as reference).
- **`_ArtSource/` is git-ignored.** A fresh clone will not have raw art, reference images, or "pending" unused art. Do not assume it exists; do not add gameplay-critical assets there.
- **Unity batch-mode builds fail if the Unity Editor is already open** on this project (Hub alone is fine). If a build command mysteriously fails, check for a stray open Editor instance first.
- **Editor-script API names drift between Unity versions — check before guessing.** `PlayerSettings.GetSupportedPlatformIconKinds` does not exist here; it is `GetSupportedIconKinds`. And `AndroidPlatformIconKind` lives in the Android module's own assembly, which an Editor script does not reference by default, so icon kinds have to be *enumerated* rather than named. Both facts cost a failed build to discover.
- **Texture2D is bottom-up; source art read via `LoadImage` is too.** Flipping V when blitting one into the other stands the image on its head — which is exactly how the first generated app icon came out upside down. There is no flip needed between two `Texture2D`s.
- **`PuzzleGame.TotalStars()`/`StarsFor()` are served from an in-memory cache, not PlayerPrefs.** They used to hit PlayerPrefs directly, and because the level picker asks about every level while the zoo asks for the running total once per animal, the picker was doing **~34,000 lookups and ~34,000 string allocations per frame** — a felt stutter and a hot phone, three times worse after the jump from 40 to 130 levels. Anything that writes `zoo_stars_*` behind the cache's back (the screenshot tour, any future cheat) **must** call `PuzzleGame.ReloadProgress()`; both scenes call it in `Start`.
- **IMGUI scroll views cannot be dragged.** `GUI.BeginScrollView` responds only to its scrollbar and the mouse wheel, so on a phone the level path could not be scrolled *at all* — the one thing that screen exists for. `MainMenu.UpdateFlickScroll` hand-rolls drag + inertia from `Update` (not `OnGUI`, which never sees the drag), and taps are suppressed while `Dragged` so a flick doesn't also launch a level.
- **Full-screen panels, not centred cards.** Zoo/Levels/Settings go through `FullScreenPanel`. As small dialogs over the landing page they wasted most of a tall phone and read as popups rather than places. Anything drawn inside must size itself from the returned body rect — several bugs came from pixel offsets tuned for the old 210px-wide cards.
- **Every animal carries a signature-colour glow at `sortingOrder 5`, scale 1.34.** Anything else drawn behind an animal has to clear that glow or it is simply invisible. The heavy sleeper's shadow was drawn at order 4 and scale 1.30 — smaller than the glow and behind it — so for all of chapter 7 the one animal that *cannot move on its own* looked identical to every other animal. It's now a flat shadow pushed down to the floor of the cell; sitting it behind the animal isn't enough either, because a grey shadow behind a grey animal just reads as more animal.
- **`ScreenCapture.CaptureScreenshot(path, 2)` supersamples correctly here**, IMGUI included, so store screenshots come out at 1216×2160 from a 608×1080 window. Note the standalone player **clamps a windowed build to the monitor height** — asking for 1080×1920 on a 1080p screen silently gives a square 1080×1080, so pick a window that fits and let the supersize do the work.
- **`ArtImportSettings.cs` force-sets all `Resources/Art/*` textures to Uncompressed.** This keeps sprite quality high but means large PNGs bloat the build significantly — keep background images downscaled (this was a real issue addressed in an earlier commit, per `docs/ASSETS_AND_TODO.md`'s now-outdated background list).
- **PlayerPrefs keys in active use:** `zoo_level` (last/selected level index), `zoo_furthest` (resume point), `zoo_stars_<i>` (best stars per level index), `zoo_tutorial_done` / `zoo_taught_ch<n>` (per-chapter walkthrough shown), `sound_on` (0/1), `haptics_on` (0/1), `zoo_seen` (arrivals already announced), `zoo_want_daily` (menu → puzzle scene handoff, cleared on read), `night_last` / `night_streak` / `night_best` / `night_total` (Tonight's Puzzle), `chonky_best` (leftover from the old merge game — still read by the vestigial `HighScore` panel, TODO #3). Changing any of these keys' names will silently reset player progress.
- **Every save key must also be listed in `SaveGuard.Keys()`.** PlayerPrefs cannot be enumerated in Unity, so that method IS the save format — a key added to the game but not to `Keys()` compiles fine, works fine, and is silently missing from the backup that restores a wiped save.
- **Levels live in TWO generated arrays now:** `Levels` in `PuzzleGame.cs` (the 130-level campaign) and `Dailies` in `DailyLevels.cs` (the nightly pool). They're separate files on purpose — the generator rewrites one array per file, so a tool never has to find the right array inside a file holding two. `PuzzleGame` is `partial` for exactly this reason.
- **Never regenerate a chapter by splicing into the existing array.** If a chapter fails to generate, a splice silently leaves a HOLE, and because chapters are addressed by index (`ChapterStart`) every later chapter shifts up and gets played under the wrong chapter's rules — a level-breaking bug that looks fine in a diff. This actually happened to chapter 5. Use `tools/assemble_levels.py`, which rebuilds the whole array from the plan plus the last commit and refuses to write unless the count is exactly `sum(CHAPTER_LEN)`.
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
