using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChonkyMerge
{
    /// <summary>
    /// The landing page. Builds the themed background, bobbing critters, logo, and
    /// tappable buttons (Play, High Score, Settings) plus a share and sound toggle —
    /// all from the generated art, with no manual scene wiring.
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        private Camera _cam;
        private float H, W, ContentW;
        private enum Panel { None, HighScore, Settings, Levels, Zoo }
        private Panel _panel = Panel.None;

        private readonly System.Collections.Generic.List<Transform> _floaters = new();
        private readonly System.Collections.Generic.List<Vector3> _floaterBase = new();
        private GUIStyle _title, _big, _big2, _mid, _btn, _cellNum, _pill, _bigPill, _note, _noteLight, _cellSub, _cellSub2, _cellSub3, _chapTitle;
        private Texture2D _pillTex, _starTex, _dimTex, _cardTex, _dotTex;
        private Vector2 _levelScroll, _zooScroll;
        private float _levelsCenterY, _levelsW, _levelsH;   // world footprint of the big Levels button
        // the "this level is star-locked" helper strip: which gate was tapped, where to
        // send them for the missing stars, and how long the strip stays up
        private bool _scrollToCurrent = true;
        private int _gateLevel, _topUpLevel;
        private float _gateTime;

        private void Start()
        {
            SleepyZoo.PuzzleGame.ReloadProgress();
            SetupCamera();
            H = _cam.orthographicSize;
            W = H * _cam.aspect;
            ContentW = Mathf.Min(2f * W, 2f * H * 0.64f); // keep a portrait content column

            BuildBackground();
            BuildFloaters();
            BuildLogo();
            BuildButtons();

            if (ShotArg("-shots") != null) StartCoroutine(ShotTour());
        }

        // ---- screenshot tour (development only) ----
        // Every screen in this game is IMGUI drawn in code, so "does it look right?"
        // can only be answered by rendering it. Run the Windows build with
        //     WobbleZoo.exe -shots C:\some\folder -shotstars 26
        // and it walks the menu, the blanket path and the zoo, saving a PNG of each,
        // then exits. `-shotstars` fakes progress so locked screens can be seen too;
        // it is never written to disk (the process is killed rather than quit, so
        // Unity never flushes PlayerPrefs) — a real player's save is untouchable.
        private static string ShotArg(string flag)
        {
            var a = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == flag) return a[i + 1];
            for (int i = 0; i < a.Length; i++) if (a[i] == flag) return "";
            return null;
        }

        private System.Collections.IEnumerator ShotTour()
        {
            string dir = ShotArg("-shots");
            if (string.IsNullOrEmpty(dir)) dir = ".";
            // Without this the player stops rendering the moment its window loses focus,
            // the coroutine stops getting frames, and the tour silently stalls part-way
            // through — leaving a folder of three screenshots and no error anywhere.
            Application.runInBackground = true;
            string starArg = ShotArg("-shotstars");
            if (!string.IsNullOrEmpty(starArg) && int.TryParse(starArg, out int upto))
            {
                // in-memory only: two stars on everything cleared, so gates, the path
                // and a few zoo arrivals all have something to show
                for (int i = 0; i < upto; i++) PlayerPrefs.SetInt("zoo_stars_" + i, i % 3 == 0 ? 3 : 2);
                PlayerPrefs.SetInt("zoo_furthest", upto);
                SleepyZoo.PuzzleGame.ReloadProgress();   // written behind the star cache's back
            }

            // Force the arrival card on. Unity can flush PlayerPrefs behind our back, so
            // a previous tour's "mark everything seen" can survive into this one and
            // silently turn this shot into a duplicate of the home screen — which is
            // exactly what happened the first time.
            PlayerPrefs.SetInt("zoo_seen", 0);
            yield return Shot(dir, "01_arrival");
            // The arrival card covers the home screen and hides the tabs behind it, so
            // clear the pending arrivals and shoot home properly too.
            PlayerPrefs.SetInt("zoo_seen", Zoo.UnlockedCount());
            PlayerPrefs.SetInt("night_last", Nightly.Tonight - 1);   // a live streak to look at
            PlayerPrefs.SetInt("night_streak", 6);
            PlayerPrefs.SetInt("night_best", 6);
            yield return Shot(dir, "02_home");
            _panel = Panel.Levels;   yield return Shot(dir, "03_path");
            _levelScroll = new Vector2(0, 900f); yield return Shot(dir, "04_path_scrolled");
            _panel = Panel.Zoo;      yield return Shot(dir, "05_zoo");
            _zooScroll = new Vector2(0, 620f); yield return Shot(dir, "06_zoo_scrolled");
            _panel = Panel.Settings; yield return Shot(dir, "07_settings");

            // Hand over to the puzzle scene, which shoots one board per chapter and then
            // hard-kills the process — so Unity never flushes the faked PlayerPrefs.
            SceneManager.LoadScene("Puzzle");
        }

        private System.Collections.IEnumerator Shot(string dir, string name)
        {
            for (int f = 0; f < 6; f++) yield return null;      // let the panel settle
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, name + ".png"), 2);
            for (int f = 0; f < 12; f++) yield return null;     // and let the file land
        }

        private void SetupCamera()
        {
            _cam = Camera.main;
            if (_cam == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                _cam = go.AddComponent<Camera>();
            }
            _cam.orthographic = true;
            _cam.orthographicSize = 5f;
            _cam.transform.position = new Vector3(0, 0, -10);
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.98f, 0.86f, 0.77f);
        }

        // ---- builders ----
        private SpriteRenderer Load(string res, Vector2 pos, int order, out float worldH)
        {
            var s = Resources.Load<Sprite>("Art/" + res);
            var go = new GameObject(res);
            go.transform.position = new Vector3(pos.x, pos.y, 0);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s;
            sr.sortingOrder = order;
            worldH = s != null ? s.bounds.size.y : 1f;
            return sr;
        }

        private GameObject FitWidth(string res, Vector2 pos, int order, float worldWidth, out float resultH)
        {
            var sr = Load(res, pos, order, out _);
            float w = sr.sprite.bounds.size.x;
            float sc = worldWidth / w;
            sr.transform.localScale = new Vector3(sc, sc, 1);
            resultH = sr.sprite.bounds.size.y * sc;
            return sr.gameObject;
        }

        private void BuildBackground()
        {
            var sr = Load("MenuBackground", Vector2.zero, 0, out float wh);
            float ww = sr.sprite.bounds.size.x;
            float cover = Mathf.Max((2f * W) / ww, (2f * H) / wh);
            sr.transform.localScale = new Vector3(cover, cover, 1);
        }

        private void BuildFloaters()
        {
            string[] kinds = { "critter_cat", "critter_dog", "critter_capy", "critter_hamster" };
            Vector2[] spots =
            {
                new(-W * 0.66f, H * 0.24f), new(W * 0.68f, H * 0.30f),
                new(-W * 0.60f, -H * 0.30f), new(W * 0.62f, -H * 0.20f)
            };
            for (int i = 0; i < kinds.Length; i++)
            {
                var go = FitWidth(kinds[i], spots[i], 2, ContentW * 0.20f, out _);
                var sr = go.GetComponent<SpriteRenderer>();
                sr.color = new Color(1, 1, 1, 0.92f);
                _floaters.Add(go.transform);
                _floaterBase.Add(go.transform.position);
            }
        }

        private void BuildLogo()
        {
            FitWidth("Logo", new Vector2(0, H * 0.60f), 5, ContentW * 0.92f, out _);
        }

        private void BuildButtons()
        {
            float y = H * 0.06f;
            y = AddButton("btn_play", ButtonId.Play, y, ContentW * 0.86f) - 0.35f;

            // Middle slot is now a big "Levels" button. It's drawn in OnGUI (so it can
            // carry a text label) — here we just reserve its world footprint to match
            // the other pills. This replaces the old vestigial "High Score" button
            // (which showed the dead merge-game score) and the tiny top-left tab.
            float lvW = ContentW * 0.74f;
            var probe = Resources.Load<Sprite>("Art/btn_score");
            float sc = lvW / probe.bounds.size.x;
            _levelsH = probe.bounds.size.y * sc;
            _levelsW = lvW;
            _levelsCenterY = y - _levelsH * 0.5f;
            y = _levelsCenterY - _levelsH * 0.5f - 0.30f;

            AddButton("btn_settings", ButtonId.Settings, y, ContentW * 0.74f);
        }

        private float AddButton(string res, ButtonId id, float topY, float worldWidth)
        {
            // measure first
            var probe = Resources.Load<Sprite>("Art/" + res);
            float sc = worldWidth / probe.bounds.size.x;
            float hgt = probe.bounds.size.y * sc;
            float centerY = topY - hgt * 0.5f;

            var go = FitWidth(res, new Vector2(0, centerY), 10, worldWidth, out _);
            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            var mb = go.AddComponent<MenuButton>();
            mb.Setup(id, go.transform.localScale);
            return centerY - hgt * 0.5f; // bottom edge for next button
        }

        // The floating sound and share sprites used to live here. They're gone on
        // purpose: the source art has the glyph and its circle misaligned, so both
        // buttons visibly spilled outside their own rims on a real phone — and they
        // crowded the top-right corner that the Tonight pill needs. Sound already had
        // a switch in Settings, and Share has moved there too, so the landing page is
        // now just a title, three buttons, and the two tabs that lead somewhere.

        // ---- interaction ----
        private void Update()
        {
            AnimateFloaters();

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (_panel != Panel.None) { _panel = Panel.None; Sfx.Click(); }
                else Application.Quit();
                return;
            }

            // Flick scrolling is driven from Update, not OnGUI, because IMGUI never
            // delivers drag events for a scroll view's body.
            if (_panel == Panel.Levels) UpdateFlickScroll(ref _levelScroll);
            else if (_panel == Panel.Zoo) UpdateFlickScroll(ref _zooScroll);
            else { _dragging = false; _dragVel = 0f; _dragDist = 0f; }

            if (_panel != Panel.None) return; // panel taps handled by OnGUI

            if (Input.GetMouseButtonDown(0)) HandleTap(Input.mousePosition);
            for (int i = 0; i < Input.touchCount; i++)
                if (Input.GetTouch(i).phase == TouchPhase.Began)
                    HandleTap(Input.GetTouch(i).position);
        }

        private void AnimateFloaters()
        {
            for (int i = 0; i < _floaters.Count; i++)
            {
                float ph = i * 1.7f + Time.time;
                Vector3 b = _floaterBase[i];
                _floaters[i].position = b + new Vector3(Mathf.Sin(ph * 0.6f) * 0.10f,
                                                        Mathf.Sin(ph) * 0.16f, 0f);
                _floaters[i].rotation = Quaternion.Euler(0, 0, Mathf.Sin(ph * 0.8f) * 6f);
            }
        }

        private void HandleTap(Vector3 screenPos)
        {
            Vector3 wp = _cam.ScreenToWorldPoint(screenPos);
            var col = Physics2D.OverlapPoint(wp);
            if (col == null) return;
            var btn = col.GetComponent<MenuButton>();
            if (btn == null) return;

            btn.Press();
            Sfx.Click();
            DoAction(btn.Id);
        }

        private void DoAction(ButtonId id)
        {
            switch (id)
            {
                case ButtonId.Play:
                    // continue where they left off rather than restarting the tutorial
                    PlayerPrefs.SetInt("zoo_level", SleepyZoo.PuzzleGame.ResumeLevel());
                    PlayerPrefs.Save();
                    SceneManager.LoadScene("Puzzle");
                    break;
                case ButtonId.HighScore: _panel = Panel.HighScore; break;
                case ButtonId.Settings: _panel = Panel.Settings; break;
                case ButtonId.Share:
                    NativeShare.ShareText(ShareMessage);
                    break;
                case ButtonId.SoundToggle: Sfx.SoundOn = !Sfx.SoundOn; break;
            }
        }

        // ---- HUD / panels ----
        private void OnGUI()
        {
            EnsureStyles();

            // Big "Levels" button, sitting in the middle slot of the button stack.
            // Its world footprint was reserved in BuildButtons; convert to screen space
            // so it lines up perfectly with the Play/Settings pills.
            if (_panel == Panel.None && _levelsW > 0f)
            {
                Vector3 tl = _cam.WorldToScreenPoint(new Vector3(-_levelsW * 0.5f, _levelsCenterY + _levelsH * 0.5f, 0));
                Vector3 br = _cam.WorldToScreenPoint(new Vector3(_levelsW * 0.5f, _levelsCenterY - _levelsH * 0.5f, 0));
                var rLv = new Rect(tl.x, Screen.height - tl.y, br.x - tl.x, (Screen.height - br.y) - (Screen.height - tl.y));
                if (GUI.Button(rLv, "Levels", _bigPill)) { Sfx.Tap(); _panel = Panel.Levels; _scrollToCurrent = true; }
                // live star total, tucked just under the button so stars feel worth chasing
                GUI.Label(new Rect(0, rLv.yMax + 2, Screen.width, 30),
                    $"{SleepyZoo.PuzzleGame.TotalStars()} / {SleepyZoo.PuzzleGame.MaxStars} stars collected", _note);
            }

            // The zoo tab, top-left: a live count of who's home. Doubles as the badge
            // that tells the player there IS a zoo without a tutorial explaining it.
            // Hidden while an arrival card is up, so nothing is tappable behind it.
            if (_panel == Panel.None && Zoo.PendingArrival() < 0)
            {
                // One honest top bar: two tabs, same size, one at each end, both sized
                // for a thumb. They used to be a small pill on the left and a pill on
                // the right that printed straight over the share icon.
                float barY = Screen.height - Screen.safeArea.height - Screen.safeArea.y + 18;
                float tabW = Mathf.Min(230f, Screen.width * 0.40f), tabH = 76f;

                var rZoo = new Rect(18, barY, tabW, tabH);
                if (GUI.Button(rZoo, $"Zoo  {Zoo.UnlockedCount()}/{Zoo.Count}", _pill))
                { Sfx.Tap(); _panel = Panel.Zoo; _zooScroll = Vector2.zero; }

                // Tonight's Puzzle, mirrored on the right. One tab, no second map and no
                // badge shouting at anyone — a daily thing that nags is a daily thing
                // people turn off. It says what it is and waits.
                if (Nightly.Available)
                {
                    var rNight = new Rect(Screen.width - tabW - 18, barY, tabW, tabH);
                    if (GUI.Button(rNight, Nightly.DoneTonight ? "Tonight  done" : "Tonight", _pill))
                    {
                        Sfx.Tap();
                        PlayerPrefs.SetInt(SleepyZoo.PuzzleGame.DailyRequestKey, 1);
                        PlayerPrefs.Save();
                        SceneManager.LoadScene("Puzzle");
                    }
                    // Caption sits under the tab, right-aligned, and is kept short — the
                    // full story is told on the win panel, not on the home screen.
                    var cap = _note.alignment;
                    _note.alignment = TextAnchor.UpperRight;
                    GUI.Label(new Rect(rNight.xMax - 320, rNight.yMax + 4, 320, 28),
                              Nightly.Line(), _note);
                    _note.alignment = cap;
                }
            }

            if (_panel == Panel.Levels) { DrawLevelsPanel(); return; }
            if (_panel == Panel.Zoo) { DrawZooPanel(); return; }
            if (_panel == Panel.None && DrawArrivalCard()) return;

            if (_panel == Panel.None) return;

            // Settings. The old "High Score" panel and its "Reset high score" button are
            // gone: they belonged to the merge game this project used to be, read from a
            // score this game never writes, and were unreachable from the menu anyway.
            var body = FullScreenPanel("Settings", out bool close);
            if (close) { Sfx.Click(); _panel = Panel.None; return; }

            float bw = Mathf.Min(460f, Screen.width * 0.82f);
            float bh = 84f, gap = 18f;
            float bx = (Screen.width - bw) * 0.5f;
            float y = body.y + 40f;

            if (GUI.Button(new Rect(bx, y, bw, bh), Sfx.SoundOn ? "Sound:  ON" : "Sound:  OFF", _pill))
            { Sfx.SoundOn = !Sfx.SoundOn; Sfx.Click(); }
            y += bh + gap;

            // Vibration is its own switch, not part of Sound: playing muted with
            // the animals still landing in your hand is a real way people play.
            if (GUI.Button(new Rect(bx, y, bw, bh), Haptics.Enabled ? "Vibration:  ON" : "Vibration:  OFF", _pill))
            {
                Haptics.Enabled = !Haptics.Enabled;
                Sfx.Tap();
                if (Haptics.Enabled) Haptics.Soft();     // let them feel what they just turned on
            }
            y += bh + gap;

            // Share used to be a floating icon in the corner whose glyph spilled out of
            // its own circle. It's a button with a word on it now.
            if (GUI.Button(new Rect(bx, y, bw, bh), "Tell a friend", _pill))
            { Sfx.Click(); NativeShare.ShareText(ShareMessage); }
            y += bh + gap * 2f;

            GUI.Label(new Rect(0, y, Screen.width, 30),
                      $"Wobble Zoo  {Application.version}", _cellSub3);
        }

        // Plain sentences only. The old copy used an em dash and it came through as a
        // stray glyph in the share sheet on a real phone.
        private const string ShareMessage =
            "I'm playing Wobble Zoo, a cozy bedtime puzzle where one swipe slides every "
            + "animal until something stops them. You just tuck them all into bed. "
            + "It's very relaxing.";

        // ---- the zoo ----
        // A single wide bedroom you scroll sideways: a bed per animal, filled in as
        // they arrive, empty (and honest about the price) where the next ones will go.
        // No cards to collect, no shop, nothing to spend — just a room that fills up.
        // Every panel sits on this: a solid, warm night-coloured card with a soft
        // cream rim. The default IMGUI box is translucent, which let the logo and the
        // Play button show straight through the level picker and the zoo.
        private void DrawPanelBox(Rect r)
        {
            GUI.color = new Color(0.90f, 0.84f, 0.74f, 0.55f);          // rim
            GUI.DrawTexture(new Rect(r.x - 3, r.y - 3, r.width + 6, r.height + 6), _dimTex);
            GUI.color = new Color(0.17f, 0.13f, 0.22f, 0.995f);         // body
            GUI.DrawTexture(r, _dimTex);
            GUI.color = Color.white;
        }

        /// The whole screen, not a card floating on it.
        ///
        /// The zoo and the level path used to be small dialogs centred over the landing
        /// page: on a tall phone that wasted most of the display, made the level path
        /// about six pillows tall, and read as "a popup" rather than "a place you went".
        /// These are screens. They get the screen.
        private Rect FullScreenPanel(string title, out bool close)
        {
            var sa = Screen.safeArea;
            var r = new Rect(0, 0, Screen.width, Screen.height);
            GUI.color = new Color(0.15f, 0.12f, 0.20f, 1f);
            GUI.DrawTexture(r, _dimTex);
            GUI.color = Color.white;

            float top = Screen.height - (sa.y + sa.height);

            // Two rows rather than one. Sharing a row with Back meant the title either
            // ran underneath it or wrapped and clipped, depending on screen width —
            // there is no single centred rect that survives both.
            float bw = Mathf.Min(150f, Screen.width * 0.30f);
            close = GUI.Button(new Rect(18, top + 18, bw, 70), "Back", _pill);

            bool wrapped = _big.wordWrap; _big.wordWrap = false;
            GUI.Label(new Rect(0, top + 96, Screen.width, 56), title, _big);
            _big.wordWrap = wrapped;

            const float headH = 190f;   // Back row + title + room for the caller's subtitle
            return new Rect(0, top + headH, Screen.width, sa.y + sa.height - top - headH);
        }

        // ---- flick scrolling ----
        // IMGUI scroll views only respond to their scrollbar and the mouse wheel, so on
        // a phone the level path simply could not be scrolled at all — the one thing the
        // screen exists for. These fields drive a hand-rolled drag with inertia.
        private bool _dragging;
        private float _dragLastY, _dragVel, _dragDist;
        private Rect _scrollView;          // where dragging is allowed, in GUI space
        private float _scrollMax;          // content height minus view height

        /// True while the finger has travelled far enough that this is a scroll, not a
        /// tap — used to stop a flick from also opening whatever was under the finger.
        private bool Dragged => _dragDist > 14f;

        private void UpdateFlickScroll(ref Vector2 scroll)
        {
            if (_scrollMax <= 0f) { scroll.y = 0f; _dragVel = 0f; return; }
            var p = Input.mousePosition;
            var gui = new Vector2(p.x, Screen.height - p.y);

            if (Input.GetMouseButtonDown(0) && _scrollView.Contains(gui))
            { _dragging = true; _dragLastY = p.y; _dragVel = 0f; _dragDist = 0f; }
            else if (_dragging && Input.GetMouseButton(0))
            {
                float dy = p.y - _dragLastY;
                _dragLastY = p.y;
                _dragDist += Mathf.Abs(dy);
                scroll.y += dy;                       // finger up reveals what's below
                if (Time.deltaTime > 0f) _dragVel = dy / Time.deltaTime;
            }
            else if (Input.GetMouseButtonUp(0)) _dragging = false;

            if (!_dragging)
            {
                scroll.y += _dragVel * Time.deltaTime;
                _dragVel = Mathf.MoveTowards(_dragVel, 0f, 2600f * Time.deltaTime);
                if (Mathf.Abs(_dragVel) < 8f) _dragVel = 0f;
            }
            // a soft stop at both ends rather than a hard clamp mid-flick
            if (scroll.y < 0f) { scroll.y = 0f; _dragVel = 0f; }
            if (scroll.y > _scrollMax) { scroll.y = _scrollMax; _dragVel = 0f; }
        }

        private void DrawZooPanel()
        {
            var body = FullScreenPanel("Your zoo", out bool close);
            if (close) { Sfx.Tap(); _panel = Panel.None; return; }

            int home = Zoo.UnlockedCount();
            GUI.Label(new Rect(0, body.y - 38, Screen.width, 32),
                      home == 1 ? "1 friend has moved in" : $"{home} friends have moved in", _noteLight);

            // A two-column grid that scrolls downward, rather than one row that scrolled
            // sideways. Sideways scrolling in a list is something people simply don't try
            // on a phone, so half the zoo was effectively invisible.
            float pad = 22f;
            float innerW = body.width - pad * 2;
            float bedW = (innerW - 18f) * 0.5f;
            float bedH = bedW * 1.30f;
            int rows = (Zoo.Count + 1) / 2;

            // Reserve room under the grid for the "who's next" line, the dream (which
            // wraps to two lines on a narrow screen) and the lanterns. Under-reserving
            // here is what cut "…and answers them too" in half.
            float footer = 150f + (Nightly.Available ? 74f : 0f);
            var view = new Rect(body.x + pad, body.y + 6, innerW, body.height - footer - 12f);
            float contentH = rows * (bedH + 16f);
            _scrollView = view;
            _scrollMax = Mathf.Max(0f, contentH - view.height);

            GUI.BeginScrollView(view, _zooScroll, new Rect(0, 0, innerW, contentH),
                                GUIStyle.none, GUIStyle.none);
            for (int i = 0; i < Zoo.Count; i++)
            {
                float bx = (i % 2) * (bedW + 18f);
                float by = (i / 2) * (bedH + 16f);
                if (by + bedH < _zooScroll.y || by > _zooScroll.y + view.height) continue;
                DrawBed(new Rect(bx, by, bedW, bedH), i);
            }
            GUI.EndScrollView();

            // one line, always: who's next and exactly how far away they are
            var line = new Rect(body.x + pad, view.yMax + 12, innerW, 44);
            GUI.Label(line, Zoo.NextLine(), _noteLight);

            // and the dream, for anyone who's settled all the way in
            int dreaming = -1;
            for (int i = Zoo.Count - 1; i >= 0; i--) if (Zoo.Stage(i) >= 2) { dreaming = i; break; }
            if (dreaming >= 0)
                GUI.Label(new Rect(body.x + pad, line.yMax + 2, innerW, 72),
                          $"{Zoo.Pals[dreaming].name} {Zoo.Pals[dreaming].dream}.", _cellSub3);

            if (Nightly.Available)
                DrawLanterns(new Rect(body.x + pad, body.yMax - 62f, innerW, 44f));
        }

        /// The streak made visible: one lantern per milestone, lit ones warm and
        /// glowing, the rest dark. This is what nights actually buy — the zoo getting
        /// cosier — so it has to be something you can point at, not a number.
        private void DrawLanterns(Rect r)
        {
            int lit = Nightly.Lanterns, n = Nightly.MaxLanterns;
            float step = Mathf.Min(38f, r.width / (n + 1f));
            float d = step * 0.62f;
            float x = r.x + (r.width - step * (n - 1) - d) * 0.5f;
            float y = r.y + 4f;
            for (int i = 0; i < n; i++)
            {
                var c = i < lit ? new Color(1.00f, 0.80f, 0.42f, 0.95f)
                                : new Color(1.00f, 1.00f, 1.00f, 0.13f);
                if (i < lit)   // a soft halo, so a lit lantern actually reads as lit
                {
                    GUI.color = new Color(1.00f, 0.78f, 0.36f, 0.22f);
                    GUI.DrawTexture(new Rect(x + i * step - d * 0.35f, y - d * 0.35f, d * 1.7f, d * 1.7f), _dotTex);
                }
                GUI.color = c;
                GUI.DrawTexture(new Rect(x + i * step, y, d, d), _dotTex);
            }
            GUI.color = Color.white;
            GUI.Label(new Rect(r.x, y + d + 2f, r.width, 26f),
                      lit >= n ? "every lantern lit" : $"{lit} of {n} lanterns lit", _cellSub3);
        }

        // One animal, in bed. Built bottom-up from the pillow so nothing can drift
        // into the name: coloured blanket, cream bed (the same art the board uses),
        // animal resting on it, name and how settled they are underneath.
        private void DrawBed(Rect r, int i)
        {
            var pal = Zoo.Pals[i];
            bool home = Zoo.Unlocked(i);
            int stage = Zoo.Stage(i);
            float cx = r.x + r.width * 0.5f;

            // Everything here is a fraction of the card, not a pixel count. The old
            // fixed offsets were tuned for a 210px bed in a small dialog; on the
            // full-screen grid the cards are twice that and the animals grew straight
            // out of their beds.
            float labelH = r.height * 0.27f;                 // name + how settled they are
            float pw = r.width * 0.66f, ph = pw * 0.58f;
            var pillow = new Rect(cx - pw * 0.5f, r.yMax - labelH - ph, pw, ph);

            // the blanket: a soft disc in the animal's signature colour, wider than the
            // bed so a rim of "whose bed is this" always shows around the cream
            var blanket = new Rect(pillow.x - pw * 0.22f, pillow.y - ph * 0.42f,
                                   pillow.width + pw * 0.44f, pillow.height + ph * 0.84f);
            GUI.color = home ? new Color(pal.col.r, pal.col.g, pal.col.b, 0.85f)
                             : new Color(0.55f, 0.52f, 0.58f, 0.28f);
            GUI.DrawTexture(blanket, _dotTex);
            GUI.color = Color.white;

            // a special friend sleeps under a gold rim; a rare guest gets a wider glow
            if (home && pal.tier != Zoo.Tier.Friend)
            {
                float g = pal.tier == Zoo.Tier.Guest ? r.width * 0.15f : r.width * 0.07f;
                GUI.color = new Color(1f, 0.86f, 0.42f, pal.tier == Zoo.Tier.Guest ? 0.30f : 0.22f);
                GUI.DrawTexture(new Rect(blanket.x - g, blanket.y - g, blanket.width + g * 2, blanket.height + g * 2), _dotTex);
                GUI.color = Color.white;
            }

            if (_cardTex != null)
            {
                GUI.color = home ? new Color(1f, 1f, 1f, 0.95f) : new Color(0.7f, 0.68f, 0.72f, 0.35f);
                GUI.DrawTexture(pillow, _cardTex);
                GUI.color = Color.white;
            }

            if (home)
            {
                var art = Zoo.Art(i);
                if (art != null)
                {
                    // a settled animal curls up smaller and sinks further into the bed
                    float k = stage >= 1 ? 0.44f : 0.50f;
                    float aw = r.width * k, ah = aw * art.height / (float)art.width;
                    float breathe = 1f + Mathf.Sin(Time.time * (stage >= 1 ? 1.1f : 1.7f) + i) * (stage >= 1 ? 0.022f : 0.012f);
                    aw *= breathe; ah *= breathe;
                    // sit them IN the bed: their bottom rests part-way down the pillow
                    float tuck = stage >= 1 ? 0.52f : 0.42f;
                    GUI.DrawTexture(new Rect(cx - aw * 0.5f, pillow.y + ph * tuck - ah, aw, ah), art);
                }
                GUI.Label(new Rect(r.x, r.yMax - labelH + 2f, r.width, labelH * 0.52f), pal.name, _big2);
                GUI.color = new Color(1f, 1f, 1f, 0.72f);
                GUI.Label(new Rect(r.x, r.yMax - labelH * 0.46f, r.width, labelH * 0.46f),
                          Zoo.StageWord(stage), _cellSub3);
                GUI.color = Color.white;
            }
            else
            {
                GUI.color = new Color(1f, 1f, 1f, 0.40f);
                GUI.Label(new Rect(r.x, pillow.y - r.height * 0.18f, r.width, r.height * 0.22f), "?", _title);
                GUI.color = Color.white;
                GUI.Label(new Rect(r.x + 4, r.yMax - labelH, r.width - 8, labelH), Zoo.Requirement(i), _cellSub3);
            }
        }

        // The arrival moment: one card, once, the first time you're back on the menu
        // after a friend moved in. This is the whole reason the zoo exists — the game
        // has something to show you that happened while you were away.
        private bool DrawArrivalCard()
        {
            int i = Zoo.PendingArrival();
            if (i < 0) return false;
            var pal = Zoo.Pals[i];

            GUI.color = new Color(0, 0, 0, 0.66f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _dimTex);
            GUI.color = Color.white;

            // Laid out top-down from one running cursor, and the panel is sized to fit
            // it — a fixed height had the "Say hello" button printing straight through
            // the line that says who moved in.
            float w = Mathf.Min(Screen.width * 0.84f, 560);
            float bw = w * 0.52f, artH = bw * 0.72f;
            const float titleTop = 26f, titleH = 40f, gapArt = 44f, gapName = 10f,
                        nameH = 50f, subH = 44f, gapBtn = 26f, btnH = 66f, botPad = 28f;
            float h = titleTop + titleH + gapArt + artH + gapName + nameH + subH
                      + gapBtn + btnH + botPad;
            var box = new Rect((Screen.width - w) / 2, (Screen.height - h) / 2, w, h);
            DrawPanelBox(box);

            float y = box.y + titleTop;
            GUI.Label(new Rect(box.x, y, box.width, titleH), "Someone moved in!", _noteLight);
            y += titleH + gapArt;

            var blanket = new Rect(box.x + (w - bw) / 2f, y, bw, artH);
            GUI.color = new Color(pal.col.r, pal.col.g, pal.col.b, 0.85f);
            GUI.DrawTexture(blanket, _dotTex);
            GUI.color = Color.white;

            var art = Zoo.Art(i);
            if (art != null)
            {
                float aw = bw * 0.62f, ah = aw * art.height / (float)art.width;
                float bob = Mathf.Sin(Time.time * 2.2f) * 6f;
                GUI.DrawTexture(new Rect(box.x + (w - aw) / 2f, blanket.y - ah * 0.30f + bob, aw, ah), art);
            }

            y = blanket.yMax + gapName;
            GUI.Label(new Rect(box.x, y, box.width, nameH), pal.name, _big);
            y += nameH;
            GUI.Label(new Rect(box.x + 30, y, box.width - 60, subH),
                      "has come to stay at your zoo", _cellSub3);
            y += subH + gapBtn;

            if (GUI.Button(new Rect(box.x + w / 2 - 130, y, 260, btnH), "Say hello", _pill))
            {
                Sfx.Win();
                Zoo.MarkArrivalSeen();
                _panel = Panel.Zoo; _zooScroll = Vector2.zero;   // grid scrolls vertically now
            }
            return true;
        }

        // ---- level picker ----
        // Star economy lives in one place (PuzzleGame) so the menu and the game can
        // never disagree about what's unlocked.
        private static int StarsFor(int i) => SleepyZoo.PuzzleGame.StarsFor(i);
        private static bool Unlocked(int i) => SleepyZoo.PuzzleGame.IsUnlocked(i);
        private static bool GateBlocked(int i) => !Unlocked(i) && StarsFor(i - 1) > 0;

        private void DrawLevelsPanel()
        {
            var body = FullScreenPanel("Choose a level", out bool close);
            if (close) { Sfx.Click(); _panel = Panel.None; return; }
            GUI.Label(new Rect(0, body.y - 38, Screen.width, 32),
                $"{SleepyZoo.PuzzleGame.TotalStars()} / {SleepyZoo.PuzzleGame.MaxStars} stars collected", _noteLight);

            var box = body;
            float w = box.width;

            // ---- the blanket path ----
            // Levels used to be a 4-column grid: readable, but it looked like a table
            // of contents. Now each chapter is a quilted path winding up through the
            // room, one pillow per level, stitched together. Same information, but it
            // reads as somewhere you're travelling rather than a list you're ticking.
            float pad = 24f;
            float innerW = w - pad * 2;
            // Pillows are sized so that about eight always fit on screen, whatever the
            // screen is. Sizing them off the WIDTH looked right on a phone and showed
            // only four at a time in a narrow window; sizing off the height keeps the
            // path feeling like a path everywhere. Width still gets a veto.
            float pillow = Mathf.Clamp(Mathf.Min(innerW * 0.30f, body.height / 11.4f), 76f, 200f);
            float step = pillow * 1.42f;                 // vertical distance between pillows
            float headH = pillow * 0.90f, chapGap = pillow * 0.26f;

            int chapters = SleepyZoo.PuzzleGame.ChapterCount;
            float contentH = 0f;
            for (int ch = 0; ch < chapters; ch++)
            {
                int n = SleepyZoo.PuzzleGame.ChapterLastLevel(ch) - SleepyZoo.PuzzleGame.ChapterFirstLevel(ch) + 1;
                contentH += headH + (n - 1) * step + pillow + chapGap;
            }

            var view = new Rect(box.x + pad, box.y + 8, innerW, box.height - 16);
            var content = new Rect(0, 0, innerW, contentH);
            // Register the drag area and travel for Update's flick handler, and hide the
            // scrollbars — a thin IMGUI scrollbar is not a mobile control.
            _scrollView = view;
            _scrollMax = Mathf.Max(0f, contentH - view.height);
            GUI.BeginScrollView(view, _levelScroll, content, GUIStyle.none, GUIStyle.none);

            // 130 levels is a long scroll: open the picker where the player actually
            // is, not at level 1 they cleared hours ago
            if (_scrollToCurrent)
            {
                _scrollToCurrent = false;
                int here = SleepyZoo.PuzzleGame.ResumeLevel();
                float upto = 0f;
                for (int ch = 0; ch < SleepyZoo.PuzzleGame.ChapterOf(here); ch++)
                {
                    int n = SleepyZoo.PuzzleGame.ChapterLastLevel(ch) - SleepyZoo.PuzzleGame.ChapterFirstLevel(ch) + 1;
                    upto += headH + (n - 1) * step + pillow + chapGap;
                }
                int into = here - SleepyZoo.PuzzleGame.ChapterFirstLevel(SleepyZoo.PuzzleGame.ChapterOf(here));
                _levelScroll = new Vector2(0, Mathf.Clamp(upto + into * step - view.height * 0.42f, 0f, _scrollMax));
            }

            float cx = innerW * 0.5f;
            float amp = Mathf.Min(innerW * 0.5f - pillow * 0.62f, pillow * 1.5f);
            float y = 0f;
            // Only draw what's actually on screen. A pillow is roughly a dozen textures
            // and a label, so drawing all 130 of them plus their stitching was over a
            // thousand draw calls a frame for the eight or so anyone can see.
            float visTop = _levelScroll.y - step, visBot = _levelScroll.y + view.height + step;
            for (int ch = 0; ch < chapters; ch++)
            {
                int first = SleepyZoo.PuzzleGame.ChapterFirstLevel(ch);
                int lastLv = SleepyZoo.PuzzleGame.ChapterLastLevel(ch);
                bool chOpen = SleepyZoo.PuzzleGame.ChapterUnlocked(ch);
                float headTop = y;
                y += headH;
                if (headTop < visBot && headTop + headH > visTop)
                    DrawChapterHeader(headTop, innerW, ch, chOpen, headH);

                float top = y + pillow * 0.5f;
                // stitching first, so every pillow sits on top of its thread
                for (int i = first; i < lastLv; i++)
                {
                    int k = i - first;
                    float ya = top + k * step, yb = top + (k + 1) * step;
                    if (yb < visTop || ya > visBot) continue;
                    DrawStitches(new Vector2(cx + PathX(k, amp), ya),
                                 new Vector2(cx + PathX(k + 1, amp), yb),
                                 chOpen && Unlocked(i + 1));
                }
                for (int i = first; i <= lastLv; i++)
                {
                    int k = i - first;
                    var c = new Vector2(cx + PathX(k, amp), top + k * step);
                    if (c.y + pillow < visTop || c.y - pillow > visBot) continue;
                    DrawPillow(new Rect(c.x - pillow * 0.5f, c.y - pillow * 0.5f, pillow, pillow), i, chOpen);
                }
                y = top + (lastLv - first) * step + pillow * 0.5f + chapGap;
            }
            GUI.EndScrollView();
            DrawGateHelp(box);
        }

        // The path's sideways wander. Two sines so it never looks like a plain zigzag.
        private static float PathX(int k, float amp) =>
            Mathf.Sin(k * 0.85f) * amp * 0.80f + Mathf.Sin(k * 0.37f) * amp * 0.20f;

        // Dotted "stitches" joining two pillows. Unreached parts of the path are
        // faded, so how far you've come is visible at a glance.
        private void DrawStitches(Vector2 a, Vector2 b, bool reached)
        {
            const int dots = 5;
            float s = 9f;
            GUI.color = reached ? new Color(0.95f, 0.84f, 0.62f, 1f)
                                : new Color(0.72f, 0.68f, 0.72f, 0.30f);
            for (int i = 1; i <= dots; i++)
            {
                var p = Vector2.Lerp(a, b, i / (float)(dots + 1));
                GUI.DrawTexture(new Rect(p.x - s * 0.5f, p.y - s * 0.5f, s, s), _dotTex);
            }
            GUI.color = Color.white;
        }

        // One level, as a pillow on the path.
        private void DrawPillow(Rect r, int i, bool chapterOpen)
        {
            bool open = chapterOpen && Unlocked(i);
            bool gated = chapterOpen && GateBlocked(i);
            int stars = StarsFor(i);
            bool next = open && stars == 0;              // the one they're up to

            // the pillow itself
            if (next)
            {
                // a soft halo so "you are here" is obvious without an arrow or a label
                GUI.color = new Color(1f, 0.86f, 0.42f, 0.55f);
                float g = r.width * 0.30f;
                GUI.DrawTexture(new Rect(r.x - g, r.y - g, r.width + g * 2, r.height + g * 2), _dotTex);
            }
            GUI.color = open ? Color.white : new Color(0.62f, 0.60f, 0.64f, 0.5f);
            if (_cardTex != null) GUI.DrawTexture(r, _cardTex); else GUI.Box(r, GUIContent.none);
            GUI.color = Color.white;

            if (open)
            {
                GUI.Label(new Rect(r.x, r.y + r.height * 0.06f, r.width, r.height * 0.52f),
                          (i + 1).ToString(), _cellNum);
                float ss = r.width * 0.23f, sgap = ss * 0.12f, tot = 3 * ss + 2 * sgap;
                float sy = r.yMax - ss - r.height * 0.08f, sx = r.x + (r.width - tot) / 2f;
                for (int s = 0; s < 3; s++)
                {
                    GUI.color = s < stars ? Color.white : new Color(0.35f, 0.30f, 0.34f, 0.45f);
                    if (_starTex != null) GUI.DrawTexture(new Rect(sx + s * (ss + sgap), sy, ss, ss), _starTex);
                }
                GUI.color = Color.white;

                // `!Dragged` so a flick down the path doesn't also launch whatever
                // level happened to be under the finger when it lifted.
                if (GUI.Button(r, GUIContent.none, GUIStyle.none) && !Dragged)
                {
                    Sfx.Tap();
                    PlayerPrefs.SetInt("zoo_level", i); PlayerPrefs.Save();
                    SceneManager.LoadScene("Puzzle");
                }
            }
            else if (gated)
            {
                // a star checkpoint: tappable, so it can explain itself (see DrawGateHelp)
                if (GUI.Button(r, GUIContent.none, GUIStyle.none) && !Dragged)
                {
                    Sfx.Locked();
                    _gateLevel = i;
                    _topUpLevel = SleepyZoo.PuzzleGame.EasiestTopUpLevel();
                    _gateTime = 6f;
                }
                GUI.Label(new Rect(r.x, r.y + r.height * 0.04f, r.width, r.height * 0.44f),
                          (i + 1).ToString(), _cellNum);
                float ss = r.width * 0.24f;
                var sr = new Rect(r.x + r.width * 0.5f - ss - 2, r.yMax - ss - r.height * 0.12f, ss, ss);
                GUI.color = new Color(1f, 0.86f, 0.30f);
                if (_starTex != null) GUI.DrawTexture(sr, _starTex);
                GUI.color = new Color(0.36f, 0.21f, 0.10f);
                GUI.Label(new Rect(sr.xMax, sr.y - 2, r.width * 0.42f, ss + 4),
                          SleepyZoo.PuzzleGame.RequiredStars(i).ToString(), _cellSub);
                GUI.color = Color.white;
            }
            else
            {
                GUI.Label(new Rect(r.x, r.y + r.height * 0.10f, r.width, r.height * 0.5f),
                          (i + 1).ToString(), _cellNum);
            }
        }

        // A star checkpoint used to be a dead end: it said "no" and left the player
        // with nowhere to tap. Now tapping a locked level explains the gap and offers
        // the easiest level to replay for the missing stars.
        private void DrawGateHelp(Rect box)
        {
            if (_gateTime <= 0f) return;
            _gateTime -= Time.deltaTime;

            float hgt = 84f, m = 16f;
            var r = new Rect(box.x + m, box.yMax - hgt - m, box.width - m * 2, hgt);
            GUI.color = new Color(0.16f, 0.12f, 0.20f, 0.94f);
            GUI.DrawTexture(r, _dimTex);
            GUI.color = Color.white;

            int need = SleepyZoo.PuzzleGame.RequiredStars(_gateLevel) - SleepyZoo.PuzzleGame.TotalStars();
            GUI.Label(new Rect(r.x + 16, r.y + 10, r.width - 190, 30),
                      $"Level {_gateLevel + 1} opens at {SleepyZoo.PuzzleGame.RequiredStars(_gateLevel)} stars", _noteLight);
            GUI.Label(new Rect(r.x + 16, r.y + 40, r.width - 190, 30),
                      need == 1 ? "1 star to go" : $"{need} stars to go", _cellSub3);

            var br = new Rect(r.xMax - 176, r.y + 16, 160, hgt - 32);
            if (GUI.Button(br, $"Replay {_topUpLevel + 1}", _pill))
            {
                Sfx.Tap();
                PlayerPrefs.SetInt("zoo_level", _topUpLevel); PlayerPrefs.Save();
                SceneManager.LoadScene("Puzzle");
            }
        }

        // A chapter strip. A locked chapter deliberately hides its name and its
        // twist — knowing that SOMETHING changes, but not what, is what makes the
        // last few levels of the chapter before it worth grinding stars for.
        private float DrawChapterHeader(float y, float wide, int ch, bool open, float headH)
        {
            var strip = new Rect(0, y + 6f, wide, headH - 18f);
            GUI.color = open ? new Color(0.99f, 0.93f, 0.82f, 0.96f) : new Color(0.72f, 0.68f, 0.72f, 0.45f);
            GUI.DrawTexture(strip, _dimTex);
            GUI.color = new Color(0.86f, 0.74f, 0.56f, open ? 0.9f : 0.4f);
            GUI.DrawTexture(new Rect(strip.x, strip.yMax - 3f, strip.width, 3f), _dimTex);
            GUI.color = Color.white;

            string name = open ? SleepyZoo.PuzzleGame.ChapterName(ch) : "? ? ?";
            GUI.Label(new Rect(strip.x + 16, strip.y + 6, strip.width - 32, 38),
                      $"Chapter {ch + 1}  -  {name}", _chapTitle);

            if (open)
            {
                GUI.Label(new Rect(strip.x + 16, strip.y + 46, strip.width - 32, 32),
                          SleepyZoo.PuzzleGame.ChapterBlurb(ch), _cellSub);
            }
            else
            {
                int need = SleepyZoo.PuzzleGame.ChapterRequiredStars(ch);
                int have = SleepyZoo.PuzzleGame.TotalStars();
                GUI.Label(new Rect(strip.x + 16, strip.y + 44, strip.width - 32, 24),
                          SleepyZoo.PuzzleGame.ChapterTease(ch), _cellSub);
                GUI.Label(new Rect(strip.x + 16, strip.y + 68, strip.width - 32, 24),
                          $"Opens at {need} stars  -  {Mathf.Max(0, need - have)} to go", _cellSub);
            }
            return y + headH;
        }

        private void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 74, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _big = new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _mid = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _btn = new GUIStyle(GUI.skin.button) { fontSize = 24, fontStyle = FontStyle.Bold };
            _cellNum = new GUIStyle(GUI.skin.label) { fontSize = 40, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _title.normal.textColor = _big.normal.textColor = _mid.normal.textColor = Color.white;

            // Cozy pill button reused for the "Levels" tab and close/X.
            _pillTex = Resources.Load<Texture2D>("Art/ui_button");
            _pill = new GUIStyle(GUI.skin.button) { fontSize = 26, fontStyle = FontStyle.Bold, border = new RectOffset(0, 0, 0, 0), padding = new RectOffset(6, 6, 4, 8) };
            var brown = new Color(0.36f, 0.21f, 0.10f);
            _pill.normal.textColor = _pill.hover.textColor = _pill.active.textColor = brown;
            if (_pillTex != null) { _pill.normal.background = _pill.hover.background = _pill.active.background = _pillTex; }

            // Big cozy pill for the main-stack "Levels" button.
            _bigPill = new GUIStyle(GUI.skin.button) { fontSize = 46, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, border = new RectOffset(0, 0, 0, 0), padding = new RectOffset(8, 8, 6, 12) };
            _bigPill.normal.textColor = _bigPill.hover.textColor = _bigPill.active.textColor = brown;
            if (_pillTex != null) { _bigPill.normal.background = _bigPill.hover.background = _bigPill.active.background = _pillTex; }
            _cellNum.normal.textColor = brown;

            // small brown caption used for the star totals and gate labels
            _note = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _note.normal.textColor = new Color(0.40f, 0.24f, 0.12f);
            _cellSub = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, wordWrap = true };
            _cellSub.normal.textColor = brown;
            // centred twin of _cellSub, used on cream cards
            _cellSub2 = new GUIStyle(_cellSub) { alignment = TextAnchor.MiddleCenter };
            _cellSub2.normal.textColor = brown;
            // The panels are dark now, so anything drawn on one needs light text.
            var cream = new Color(0.98f, 0.94f, 0.86f);
            _cellSub3 = new GUIStyle(_cellSub) { alignment = TextAnchor.MiddleCenter };
            _cellSub3.normal.textColor = cream;
            _noteLight = new GUIStyle(_note) { };
            _noteLight.normal.textColor = new Color(1f, 0.88f, 0.60f);
            _big2 = new GUIStyle(GUI.skin.label) { fontSize = 27, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _big2.normal.textColor = cream;
            _chapTitle = new GUIStyle(GUI.skin.label) { fontSize = 25, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, wordWrap = false };
            _chapTitle.normal.textColor = brown;

            _starTex = Resources.Load<Texture2D>("Art/star_full");
            _cardTex = Resources.Load<Texture2D>("Art/tile_bed");   // warm cream card, matches the puzzle
            _dimTex = Texture2D.whiteTexture;
            _dotTex = SoftDot();

            var font = Resources.Load<Font>("Fonts/Fredoka");   // cozy rounded font, consistent with the puzzle
            if (font != null) foreach (var st in new[] { _title, _big, _big2, _mid, _btn, _pill, _bigPill, _cellNum, _note, _noteLight, _cellSub, _cellSub2, _cellSub3, _chapTitle }) st.font = font;
        }

        // A soft round dot, generated once: the path's stitching, the "you are here"
        // halo and the zoo's bed glows are all this one texture, tinted.
        private static Texture2D _softDot;
        private static Texture2D SoftDot()
        {
            if (_softDot != null) return _softDot;
            const int S = 64;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[S * S];
            float c = (S - 1) * 0.5f;
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a * (3f - 2f * a);                 // smooth edge, no hard rim
                    px[y * S + x] = new Color(1f, 1f, 1f, a);
                }
            tex.SetPixels32(px); tex.Apply();
            _softDot = tex; return tex;
        }
    }
}
