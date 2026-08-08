# Google Play listing — Tuck In

Copy-paste source for the Play Console listing. Character limits are Play's, and
every field below is already inside them.

---

## App name (max 30)

```
Tuck In: Bedtime Puzzle
```

*(26 characters. "Tuck In" alone is fine too, but the subtitle is what tells a
browsing player what the game actually is — and "puzzle" is the word people search.)*

## Short description (max 80)

```
A cozy bedtime puzzle. One swipe slides every animal. Tuck them all in.
```

*(70 characters.)*

## Full description (max 4000)

```
Every animal is sleepy. Every animal moves at once.

Swipe in any direction and the whole zoo slides that way — until something stops
them. A wall, the edge of the room, or each other. Get every animal onto a bed
and the room goes quiet.

That's the entire rule. There is nothing else to learn on level one.

A PUZZLE YOU CAN FINISH BEFORE YOU FALL ASLEEP
No level in Tuck In takes more than twelve moves to solve perfectly. Puzzles
get harder by getting cleverer, never by getting longer. You will not lose
twenty minutes to one board.

130 LEVELS, AND THE RULES KEEP MOVING
Eight chapters, and each one changes something you thought you knew:

• Sleepyheads — touch your own bed and you're asleep for good
• Musical Beds — any animal, any bed, it doesn't matter whose
• Slippery Rugs — silk you can cross but never stop on
• Honey Puddles — touch it and you stop dead, right there
• Rabbit Holes — burrows in pairs: in one, out the other, still sliding
• Heavy Sleepers — one friend too heavy to move unless somebody pushes
• The Long Night — everything at once

Every chapter starts gently with two animals, so a new idea never arrives at the
same time as a hard board.

A NEW PUZZLE EVERY NIGHT
Tonight's Puzzle is the same board for everybody, and a fresh one arrives each
night. Miss a night and you lose one night — never your whole streak. Nights
light lanterns in your zoo, and once a lantern is lit it stays lit.

A ZOO THAT FILLS UP
Ten friends move in as you play: Pip, Clover, Puddle, Biscuit, Professor,
Marzipan, Sprout, Marlow, Nutmeg and Momo. Every one of them arrives on a
schedule you can see coming — a star total, or a chapter finished. Nothing is
random. There are no boxes to open and nothing to buy. They settle in over time,
from visiting, to snuggled in, to dreaming.

MADE TO BE PLAYED IN BED
• One hand, one thumb, portrait
• Undo anything, reset anything, and a hint that shows you the very next move
• Works completely offline — on a plane, in a basement, anywhere
• Sound and vibration are separate switches, because plenty of people play muted
• No ads. No in-app purchases. No account. No timers, no lives, nothing that
  runs out and asks you to come back later.

Every single level has been solved by computer before it shipped, so the
three-star target is always genuinely reachable. No level is a lucky guess.

Goodnight.
```

---

## Categorisation

| Field | Value |
|---|---|
| App or game | **Game** |
| Category | **Puzzle** |
| Tags | brain teaser, logic puzzle, casual, relaxing, offline |
| Contains ads | **No** |
| In-app purchases | **No** |
| Target audience | Everyone, including children — see the note below |

## Content rating questionnaire — the honest answers

Every one of these is **No**: violence, blood, sexual content, nudity, profanity,
drugs, alcohol, tobacco, gambling (real or simulated), horror, crude humour,
user-to-user communication, sharing of user location, and unrestricted internet
access.

The one **Yes**: *"Does the app allow users to share content?"* — there's a Share
button that opens Android's own share sheet with a fixed sentence about the game.
It sends no user data and has no free-text field.

Expected outcome: **Everyone / PEGI 3 / ESRB E**.

## Data safety form

Answer **"No, this app does not collect or share any user data."** That is
literally true — see `store/PRIVACY.md` for why, and note the app has no
networking code of any kind, so it *cannot* transmit anything.

- Data collected: **none**
- Data shared: **none**
- Data encrypted in transit: **N/A — nothing is transmitted**
- Users can request deletion: **N/A — uninstalling removes everything**

## Permissions the build requests

- `android.permission.VIBRATE` — the small taps when an animal lands. Off in
  Settings if the player doesn't want it. This is a "normal" permission: Android
  grants it without a prompt and it needs no disclosure.

No internet permission, no storage permission, no location, no camera, no
microphone, no contacts, no advertising ID.

## Graphics checklist

| Asset | Size | Status |
|---|---|---|
| App icon | 512 × 512 PNG | ✅ `store/play_icon_512.png` |
| Feature graphic | 1024 × 500 PNG | ✅ `store/feature_graphic.png` |
| Phone screenshots | min 2, up to 8 | ✅ `store/screenshots/` |
| Tablet screenshots | optional | ❌ not made — optional, and this is a phone game |

## Suggested screenshot captions

Play doesn't take captions as a field — bake them into the image only if you want
them. In order, the shipped screenshots show:

1. The zoo you're filling up
2. One swipe, every animal moves
3. 130 levels across eight chapters
4. A new puzzle every night
5. Your friends, settling in
