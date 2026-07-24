# Sleepy Zoo — Asset Pack + TODO (handoff)

Cozy **animals-as-mechanic swipe puzzle** (Unity 6). Code: `Assets/Scripts/SleepyZoo/PuzzleGame.cs`.
Each animal moves by its own rule (cat steps, hamster rolls, bunny hops, corgi pushes, capybara
is a pushable block). Get all animals onto their beds. This file is the source of truth after a
context clear.

## Fixes to make in code (next session)
1. **Win popup** — replace the ugly semi-transparent black box with `ui_panel.png` (cozy rounded panel), larger and centered.
2. **"Par" is confusing** — relabel. Par = the move target for 3 stars. Show it as **"3★ in 6 moves"** (not "Par 6"), and on the win panel show the star thresholds.
3. **Star/move system** — keep: 3★ if moves ≤ par, 2★ if ≤ par+2, else 1★. Tune each level's par. (No hard fail limit — star-based is friendlier.)
4. **Discoverability (L4 push)** — add a **first-time ability hint** shown only on the level where an ability is introduced, e.g. L4: "Corgi pushes — swipe it into the capybara!". Keep later levels hint-free so figuring it out stays the puzzle.
5. **Buttons too small / don't match landing page** — rebuild all puzzle buttons (Menu, Undo, Reset, Next) BIG and styled with `ui_button.png` + larger font, matching the landing page's chunky buttons.
6. **Structural features to build**: level-select **map**, **animal collection** screen (repurpose the menu's empty "High Score" button), **more levels** (expand 6 → ~20+, introduce each ability slowly, multi-animal puzzles by level 6–7), **rotate backgrounds** per level-pack so it never feels repetitive, add **new animals + new abilities**.

## Asset prompt pack (generate in ChatGPT, save into Assets/Resources/Art/raw with the given names)
Backgrounds are portrait 9:16, no characters, no text, soft cozy illustrated bedtime style.
Tiles/UI/icons are square 1:1, top-down where relevant, on a **plain solid background** (or transparent) so they cut out cleanly. New animals use a **solid soft mint-green background**.

### Backgrounds (variety — rotate per level pack) → `bg_*.png`
- `bg_meadow.png`: "Cozy twilight meadow at bedtime, rolling dark-green hills, glowing fireflies, a big soft moon, deep blue starry sky, dreamy and calm, no characters, no text, vertical 9:16, soft flat cozy illustration with gentle gradients."
- `bg_treehouse.png`: "Cozy children's treehouse interior at night, warm hanging lanterns, a round window showing the moon and stars, potted plants, wooden walls, no characters, no text, vertical 9:16, soft cozy illustration."
- `bg_clouds.png`: "A dreamy pastel cloud kingdom at dusk, soft fluffy clouds, gentle stars, a calm crescent moon, peaceful bedtime mood, no characters, no text, vertical 9:16, soft flat illustration."
- `bg_library.png`: "A cozy little reading nook at night, warm lamp glow, softly blurred bookshelves, a rug, plants, calming, no characters, no text, vertical 9:16, soft cozy illustration."
- `bg_snowcabin.png`: "A cozy snowy cabin window at night, warm interior glow, soft falling snow outside, a big moon, calm and warm, no characters, no text, vertical 9:16, soft cozy illustration."

### Tiles → square, transparent background
- `tile_wall.png`: "A single cozy 'blocked' game tile for a puzzle board, a soft rounded dark wooden/stone block, gentle shadow, clearly impassable but cute, top-down, transparent background, no text, soft flat 3D style, 1:1 square."

### UI → transparent background (unless noted)
- `ui_panel.png`: "A cozy rounded pop-up dialog panel for a cute mobile game, soft cushioned/wooden card with a gentle border and soft shadow, warm inviting cream color, empty center (space for text), no text, transparent background, soft flat 3D style, 4:3."
- `ui_button.png`: "A single blank cozy rounded game button, soft tactile pill shape, warm friendly color with a soft highlight and gentle border, empty (no text), transparent background, matches a cute cozy kids game, soft flat 3D style, wide 3:1."
- `star_full.png`: "A cute filled golden star icon, soft glossy 3D, gentle glow, transparent background, no text, 1:1 square."
- `star_empty.png`: "A cute empty star icon, soft gray outline, subtle, transparent background, no text, 1:1 square."

### Level-select map → 
- `map_bg.png`: "A tall vertical cozy bedtime journey scene for a level-select map, a soft winding path through dreamy hills and clouds under a starry night sky and a big moon, calm and inviting, no text, no level markers, vertical and tall, soft cozy illustration."
- `node_open.png`: "A cute unlocked level marker button, a soft round glowing pillow/bubble, warm and inviting, transparent background, no text, 1:1 square."
- `node_locked.png`: "A cute locked level marker, a soft round button with a little sleeping 'Zzz' or closed padlock, muted/gray, transparent background, no text, 1:1 square."

### Collection screen →
- `collection_bg.png`: "A cozy bedroom shelf wall for displaying collected plush animals, warm soft lighting, empty wooden shelves, star wallpaper, no characters, no text, vertical 9:16, soft cozy illustration."
- `card_frame.png`: "A soft rounded collectible card frame to display one cute animal, gentle border and soft inner shadow, warm color, empty center, transparent background, no text, 1:1 square."
- `card_locked.png`: "A mystery collectible card, a soft rounded card with a big gentle question mark and a sleeping silhouette, muted, transparent background, no text, 1:1 square."

### New animals → square 1:1, solid soft **mint-green** background, SAME style as the existing 8
Style anchor (prefix each): "3D stylized-realistic render, Pixar / DreamWorks animation style, adorable fluffy {ANIMAL}, big soft glossy expressive eyes, ultra-soft detailed fur, chubby wholesome proportions, sitting upright and front-facing, centered, soft warm studio lighting, on a solid soft mint-green background, square 1:1, one animal only, no text."
Generate these (each will get a movement ability later):
- `animal_fox.png` (fox) · `animal_panda.png` (panda) · `animal_hedgehog.png` (hedgehog) ·
  `animal_owl.png` (owl) · `animal_deer.png` (baby deer/fawn) · `animal_duck.png` (duckling)

## Existing assets (already in the game — don't regenerate)
8 animals in `Resources/Art/animals/tier1_hamster..tier8_capybara.png`; `PuzzleBG.png` (nursery bg);
`tile.png` (cushion cell); `tile_bed.png` (bed); menu logo + buttons; app icon.
