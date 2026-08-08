# Credits & asset licences

Everything shipped in the app is either generated in code or CC0. Nothing here
requires attribution — the credits below are courtesy, and safe to publish in the
store listing.

## Sound

`Assets/Resources/Audio/` — seven clips, taken from two Kenney packs:

| In game | Fires when | Source clip | Pack |
|---|---|---|---|
| `tap.ogg` | any button | `click_002.ogg` | Interface Sounds |
| `land.ogg` | an animal skids to a stop (pitched by distance) | `drop_002.ogg` | Interface Sounds |
| `sleep.ogg` | an animal is caught by its own bed | `pluck_001.ogg` | Interface Sounds |
| `star.ogg` | each star on the win panel | `pluck_002.ogg` | Interface Sounds |
| `locked.ogg` | tapping a star-locked level | `bong_001.ogg` | Interface Sounds |
| `undo.ogg` | undo | `back_002.ogg` | Interface Sounds |
| `win.ogg` | a level is solved | `jingles_PIZZI08.ogg` | Music Jingles |

- **Kenney Interface Sounds (1.0)** and **Kenney Music Jingles** — by Kenney
  Vleugels, <https://kenney.nl>. Licence: **CC0 1.0** (public domain). Free for
  commercial use; credit appreciated, not required.
- The swipe whoosh is **generated at runtime** (`Sfx.Whoosh`) — filtered noise
  under a swell. No sample sounded like a whole room sliding at once.

Swapping any of these is a one-line change in `Sfx.cs` plus dropping a new file in
`Assets/Resources/Audio/` — the source packs are in `_ArtSource/audio_pending/`.

### Not used, and why

`_ArtSource/audio_pending/` also contains four MP3s (`Cretaceous Dawn`,
`Dentaneosuchus Hunt`, `Sauropod Spotting`, `The Britons`). These look like
commercial documentary-soundtrack recordings, and there is no licence file with
them. **They are deliberately not wired in and must not ship** unless a licence
for them is bought and recorded here. Background music is still an open slot; it
needs a CC0 or licensed loop.

## Art

- Puzzle screen visuals — board, tiles, beds, glows, arrow, buttons, and the
  painted night sky for each chapter — are **generated in code** (`PuzzleGame.cs`).
- Animal sprites (`Assets/Resources/Art/pets/`) and menu art are project-owned.
- `Assets/Resources/Art/bg_*.png` (treehouse, snow cabin, library, clouds, meadow)
  are from an older, photo-real art direction. They clash with the flat pastel
  style the game actually uses, so chapter rooms are painted in code instead.
  Left in the repo, unused.

## Fonts

All three ship inside the APK. All are SIL Open Font Licence 1.1, which permits
embedding in a commercial application; the OFL requires that they are not sold on
their own and that any modified version is not released under the reserved name.
None of that is a problem here — they are embedded unmodified.

- **Caprasimo** (Regular) — OFL 1.1. Copyright 2023 The Caprasimo Project Authors
  (https://github.com/docrepair-fonts/caprasimo-fonts). The display face: level
  numbers, animal names, screen titles.
- **Figtree** (SemiBold and Bold) — OFL 1.1. Copyright 2022 The Figtree Project
  Authors (https://github.com/erikdkennedy/figtree). Everything you read.
- **Fredoka** (SemiBold) — OFL 1.1. Superseded by the two above in the design
  pass, but kept as the fallback `Ui.cs` loads if a face is ever missing.
