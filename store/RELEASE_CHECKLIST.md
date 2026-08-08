# Publishing Tuck In to Google Play

Written for someone who has never shipped an Android app. Follow it top to bottom.

Everything in `store/` is ready to upload. The only thing that does not exist yet
is your **signing key**, and it can't — creating one means choosing a password,
which has to be yours and only yours.

---

## 1. Make your upload key (once, ever)

This is the single most important step in this document, and the only one that
cannot be undone.

Open PowerShell in the project folder and run:

```bash
& "C:\Program Files\Unity\Hub\Editor\6000.4.11f1\Editor\Data\PlaybackEngines\AndroidPlayer\OpenJDK\bin\keytool.exe" -genkeypair -v -keystore wobblezoo-upload.keystore -alias wobblezoo -keyalg RSA -keysize 2048 -validity 10000
```

It will ask you to invent a password, then repeat it, then ask for your name and
location (any of that can be left blank by pressing Enter).

> **This file and its password ARE your app.** If you lose either one, you can
> never publish an update to Tuck In again — not with a support ticket, not
> with anything. Google cannot reset it.
>
> Back up `wobblezoo-upload.keystore` somewhere that isn't this computer, and put
> the password in a password manager. Do it before you go any further.
>
> Do **not** put the keystore in this project folder's git — `.gitignore` already
> blocks `*.keystore`, and that block is deliberate.

## 2. Tell the build where the key is

In the same PowerShell window, before building:

```bash
$env:WOBBLEZOO_KEYSTORE = "C:\path\to\wobblezoo-upload.keystore"; $env:WOBBLEZOO_KEYSTORE_PASS = "the password you chose"; $env:WOBBLEZOO_KEYALIAS = "wobblezoo"; $env:WOBBLEZOO_KEYALIAS_PASS = "the password you chose"
```

These live only in that window and vanish when you close it. Nothing is written
into the project, so a password can never end up on GitHub.

## 3. Build the bundle

```bash
& "C:\Program Files\Unity\Hub\Editor\6000.4.11f1\Editor\Unity.exe" -batchmode -quit -projectPath . -executeMethod ChonkyMerge.EditorTools.StoreBuilder.BuildAab -logFile aab.log
```

Takes about ten minutes. **Close the Unity Editor first** — a batch build fails if
the editor is open on this project.

You get `Builds/TuckIn.aab`. Check `aab.log` for the line `AAB build result:
Succeeded`. If it also says **DEBUG-SIGNED**, step 2 didn't take — Play will
reject that file, so fix the variables and build again.

## 4. Set up the Play Console

1. Go to <https://play.google.com/console> and pay the one-time $25 registration
   fee. Google will verify your identity; for a personal account this can take a
   few days, so start it early.
2. **Create app** → name `Tuck In: Bedtime Puzzle`, English, **Game**, **Free**.
3. Work through the left-hand checklist. The answers are all in
   `store/LISTING.md` — app category, tags, content rating questionnaire, and the
   full description are written out ready to paste.

## 5. Upload the graphics

From the `store/` folder:

| Play asks for | Use this file |
|---|---|
| App icon (512×512) | `play_icon_512.png` |
| Feature graphic (1024×500) | `feature_graphic.png` |
| Phone screenshots (min 2) | `screenshots/` — see below |

Good screenshots to pick, in this order:

1. `screenshots/05_zoo.png` — the zoo you're filling up
2. `screenshots/08_basics.png` — a clean board
3. `screenshots/03_path.png` — 130 levels
4. `screenshots/12_honey.png` — a chapter twist
5. `screenshots/13_burrows.png` — another one
6. `screenshots/16_tonight.png` — the nightly puzzle
7. `screenshots/02_home.png` — the home screen

They're 1216×2160, which is a normal phone shape and well inside Play's limits.

## 6. Privacy policy

Play requires a **public URL**, not a file. Paste the text of `store/PRIVACY.md`
into any free host and give Play that link. Easiest options:

- A GitHub Gist set to public, then use its "Raw" URL
- A free GitHub Pages site
- A Google Site

Then in Play Console → **App content** → **Privacy policy**, paste the URL.

## 7. Data safety and app content

- **Data safety**: answer *"No, this app does not collect or share any user data."*
  That is true — the app has no networking code at all.
- **Ads**: No.
- **Content rating**: fill in the questionnaire using the answers in
  `LISTING.md`. It should come out Everyone / PEGI 3.
- **Target audience**: you may include children. Because the app collects nothing
  and has no ads or purchases, it meets the Families policy without extra work.
- **Government apps / financial features / health**: No to all.

## 8. Release it

Start with **Internal testing**, not production:

1. Play Console → **Testing → Internal testing → Create new release**
2. Upload `Builds/TuckIn.aab`
3. Add your own email as a tester, save, and use the opt-in link Play gives you
4. Install it on your phone from the Play Store and play it for a day

When you're happy: **Production → Create new release**, upload the same bundle,
and submit. First review typically takes a few days.

---

## Every time you ship an update after this

The version code must go **up** every single upload, or Play rejects it:

```bash
$env:WOBBLEZOO_VERSION = "1.0.1"; $env:WOBBLEZOO_BUILD = "2"
```

then build again with the same command as step 3. `WOBBLEZOO_VERSION` is what
players see; `WOBBLEZOO_BUILD` is the number Play counts.

---

## Things that are deliberately not done

- **No background music.** The four tracks in `_ArtSource/audio_pending/` look
  like commercial documentary music with no licence file, and shipping them would
  be a copyright problem. All the sound effects that *are* in the game are CC0
  (see `docs/CREDITS.md`). If you want music, it needs to be bought or
  commissioned properly.
- **No tablet screenshots.** Optional, and this is a phone game.
- **No colour-blind mode.** Burrow pairs are told apart by colour alone, which is
  hard for roughly 1 in 12 men. Worth fixing before you push the game hard, but
  it isn't a launch blocker.
