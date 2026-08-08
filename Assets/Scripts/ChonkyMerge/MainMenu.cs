using UnityEngine;
using UnityEngine.SceneManagement;
using TuckIn;

namespace ChonkyMerge
{
    /// <summary>
    /// Home, the map, and the dorm — the three screens outside the puzzle.
    ///
    /// This used to be a stack of sprite buttons with IMGUI panels floating over
    /// them. It is now drawn entirely through <see cref="Ui"/>, on the redesign's
    /// 390x844 canvas, which is what makes the layout survive a narrow phone and a
    /// tall one without anything colliding.
    ///
    /// The shape of the redesign, in one paragraph: home has ONE door (a big
    /// terracotta Continue that names the level you are up to), a permanent bottom
    /// rail to the three other places you can go, and nothing else competing. The
    /// map is a quilt you scroll, with each chapter as its own coloured band and
    /// locked chapters still refusing to say what changes. The dorm is a room you
    /// visit rather than a list you scroll: tap a friend and you can feed, pet or
    /// tuck them in.
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        private Camera _cam;
        private enum Screen2 { Home, Map, Dorm, Settings, Decorate }
        private Screen2 _screen = Screen2.Home;

        private float _mapScroll;
        private bool _scrollToCurrent = true;
        private int _sel = -1;              // selected dorm friend (-1 = none)
        private int _gateLevel = -1, _topUpLevel;
        private float _gateTime;
        private float _heartAt;             // the little feed/pet flourish
        private Vector2 _heartPos;
        private Color _heartCol;
        private Texture2D _heartIcon;

        private void Start()
        {
            SleepyZoo.PuzzleGame.ReloadProgress();
            SetupCamera();

            // The win screen can send the player straight to the dorm, which is the
            // only place the snacks they just earned mean anything. The request is
            // consumed here so a later, unrelated return to the menu lands home.
            if (PlayerPrefs.GetInt(SleepyZoo.PuzzleGame.MenuScreenKey, 0) == 1)
            {
                PlayerPrefs.SetInt(SleepyZoo.PuzzleGame.MenuScreenKey, 0);
                PlayerPrefs.Save();
                _screen = Screen2.Dorm;
            }

            if (ShotArg("-shots") != null) StartCoroutine(ShotTour());
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
            _cam.backgroundColor = Ui.NightTop;
        }

        // ---- screenshot tour (development only) ----
        // Every screen here is drawn in code, so "does it look right?" can only be
        // answered by rendering it. Run the Windows build with
        //     "Tuck In.exe" -shots C:\some\folder -shotstars 26
        // and it walks each screen, saves a PNG, then hands over to the puzzle scene.
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
            Application.runInBackground = true;
            string starArg = ShotArg("-shotstars");
            if (!string.IsNullOrEmpty(starArg) && int.TryParse(starArg, out int upto))
            {
                for (int i = 0; i < upto; i++) PlayerPrefs.SetInt("zoo_stars_" + i, i % 3 == 0 ? 3 : 2);
                PlayerPrefs.SetInt("zoo_furthest", upto);
                SleepyZoo.PuzzleGame.ReloadProgress();
            }

            PlayerPrefs.SetInt("zoo_seen", 0);
            yield return Shot(dir, "01_arrival");
            PlayerPrefs.SetInt("zoo_seen", Zoo.UnlockedCount());
            PlayerPrefs.SetInt("night_last", Nightly.Tonight - 1);
            PlayerPrefs.SetInt("night_streak", 6);
            PlayerPrefs.SetInt("night_best", 6);
            yield return Shot(dir, "02_home");
            _screen = Screen2.Map; _scrollToCurrent = true; yield return Shot(dir, "03_map");
            _mapScroll += 900f; yield return Shot(dir, "04_map_scrolled");
            _screen = Screen2.Dorm; yield return Shot(dir, "05_dorm");
            _sel = 0; yield return Shot(dir, "06_dorm_friend");
            _sel = -1;
            _screen = Screen2.Settings; yield return Shot(dir, "07_settings");
            _screen = Screen2.Decorate; yield return Shot(dir, "08_decorate");

            SceneManager.LoadScene("Puzzle");
        }

        private System.Collections.IEnumerator Shot(string dir, string name)
        {
            for (int f = 0; f < 6; f++) yield return null;
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, name + ".png"), 2);
            for (int f = 0; f < 12; f++) yield return null;
        }

        // ---- input ----
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (_sel >= 0) { _sel = -1; Sfx.Click(); }
                else if (_screen == Screen2.Decorate) { _screen = Screen2.Dorm; Sfx.Click(); }
                else if (_screen != Screen2.Home) { _screen = Screen2.Home; Sfx.Click(); }
                else Application.Quit();
                return;
            }
            if (_screen == Screen2.Map) UpdateFlickScroll(ref _mapScroll);
            else { _dragging = false; _dragVel = 0f; _dragDist = 0f; }
        }

        // ---- flick scrolling ----
        // IMGUI scroll views only respond to their scrollbar and the mouse wheel, so
        // on a phone the map simply could not be scrolled at all. This is a
        // hand-rolled drag with inertia, driven from Update because IMGUI never
        // delivers drag events for a scroll view's body.
        private bool _dragging;
        private float _dragLastY, _dragVel, _dragDist, _scrollMax;
        private Rect _scrollView;
        private bool Dragged => _dragDist > 14f;

        private void UpdateFlickScroll(ref float scroll)
        {
            if (_scrollMax <= 0f) { scroll = 0f; _dragVel = 0f; return; }
            var p = Input.mousePosition;
            var gui = new Vector2(p.x, UnityEngine.Screen.height - p.y);

            if (Input.GetMouseButtonDown(0) && _scrollView.Contains(gui))
            { _dragging = true; _dragLastY = p.y; _dragVel = 0f; _dragDist = 0f; }
            else if (_dragging && Input.GetMouseButton(0))
            {
                float dy = p.y - _dragLastY;
                _dragLastY = p.y;
                _dragDist += Mathf.Abs(dy);
                scroll -= dy / Ui.S;
                if (Time.deltaTime > 0f) _dragVel = -dy / Ui.S / Time.deltaTime;
            }
            else if (Input.GetMouseButtonUp(0)) _dragging = false;

            if (!_dragging)
            {
                scroll += _dragVel * Time.deltaTime;
                _dragVel = Mathf.MoveTowards(_dragVel, 0f, 2600f * Time.deltaTime);
                if (Mathf.Abs(_dragVel) < 8f) _dragVel = 0f;
            }
            if (scroll < 0f) { scroll = 0f; _dragVel = 0f; }
            if (scroll > _scrollMax) { scroll = _scrollMax; _dragVel = 0f; }
        }

        // ---- the frame ----
        private void OnGUI()
        {
            Ui.Frame();
            switch (_screen)
            {
                case Screen2.Map: DrawMap(); break;
                case Screen2.Dorm: DrawDorm(); break;
                case Screen2.Settings: DrawSettings(); break;
                case Screen2.Decorate: DrawDecorate(); break;
                default: DrawHome(); break;
            }
        }

        // =====================================================================
        // 1a — HOME
        // One door, one hero, no collisions.
        // =====================================================================
        private void DrawHome()
        {
            float H = Ui.H;

            Ui.Backdrop(10, (0f, Ui.NightTop), (0.34f, Ui.NightMid), (0.62f, Ui.NightWarm),
                        (0.84f, Ui.NightGlow), (1f, Ui.NightLow));

            // The moon, with its long soft bloom. It is anchored to the design's
            // y=258 but never allowed to sink behind the dorm shelf — on a short
            // phone the shelf rides up, and a moon peeking out from behind a
            // bookcase reads as a bug rather than a sky.
            float shelfY = H - 300f - 150f;
            float moonY = Mathf.Min(258f, shelfY - 96f);
            Ui.Glow(Ui.R(298, moonY, 70, 70), 46f, new Color(1f, 0.886f, 0.698f, 0.34f));
            Ui.Circle(Ui.R(298, moonY, 70, 70), Ui.Hex(0xffeecd));
            Twinkles();

            // ground: a soft hill, then the dorm shelf standing on it
            Ui.Hill(H - 236f - 90f, 90f, Ui.Hex(0x5b3a1e));

            // ---- top row: progress on the left, quiet controls on the right ----
            int stars = SleepyZoo.PuzzleGame.TotalStars();
            Ui.Chip(18, 58, 32, stars.ToString(), Ui.StarTex, Ui.StarLit, Ui.Hex(0xfff2e2),
                    Ui.Ghost(0.14f), 15f, "/ " + SleepyZoo.PuzzleGame.MaxStars);

            if (Ui.GhostDisc(Ui.W - 18 - 38 - 47, 55, 38, Sfx.SoundOn ? Icons.Speaker : Icons.SpeakerOff, 0.45f))
            { Sfx.SoundOn = !Sfx.SoundOn; Sfx.Click(); }
            if (Ui.GhostDisc(Ui.W - 18 - 38, 55, 38, Icons.Gear, 0.45f))
            { Sfx.Click(); _screen = Screen2.Settings; }

            // ---- the name ----
            GUI.Label(Ui.R(0, 132, Ui.W, 66), "Tuck", Ui.Head(70, Ui.Hex(0xfff4e4)));
            GUI.Label(Ui.R(0, 196, Ui.W, 66), "In", Ui.Head(70, Ui.Hex(0xffcf8d)));
            GUI.Label(Ui.R(0, 268, Ui.W, 20), Ui.Track("a bedtime puzzle"),
                      Ui.Bold(12.5f, new Color(1f, 0.925f, 0.816f, 0.6f)));

            // ---- the dorm shelf: a window into the other screen ----
            DrawShelf(shelfY);

            // ---- the one primary: continue ----
            int lv = SleepyZoo.PuzzleGame.ResumeLevel();
            int ch = SleepyZoo.PuzzleGame.ChapterOf(lv);
            float by = H - 96f - 68f;
            if (ContinueButton(22, by, Ui.W - 44, 68, lv, ch))
            {
                Sfx.Click();
                PlayerPrefs.SetInt("zoo_level", lv);
                PlayerPrefs.Save();
                SceneManager.LoadScene("Puzzle");
            }

            // ---- the bottom rail ----
            DrawNav(H - 26f - 62f);

            // the arrival card sits over everything, once
            DrawArrivalCard();
        }

        private void Twinkles()
        {
            // a handful of stars that breathe out of phase, so the sky is never static
            float[,] t = { { 44, 78, 4 }, { 120, 132, 3 }, { 196, 64, 3 }, { 330, 96, 3 },
                           { 76, 176, 3 }, { 258, 152, 4 }, { 20, 232, 3 }, { 352, 214, 3 } };
            for (int i = 0; i < t.GetLength(0); i++)
            {
                float a = 0.35f + 0.5f * (0.5f + 0.5f * Mathf.Sin(Time.time * (1.6f + i * 0.23f) + i));
                Ui.Circle(Ui.R(t[i, 0], t[i, 1], t[i, 2], t[i, 2]), new Color(1f, 0.914f, 0.784f, a));
            }
        }

        /// The dorm shelf on the home screen: four cubbies, filled with whoever is
        /// actually home. It is the only place stars visibly buy something without
        /// the player having to go and look.
        private void DrawShelf(float y)
        {
            Ui.Round(Ui.R(28, y - 26, 334, 34), 14, Ui.Hex(0xa9682f));           // the rail above
            Ui.RoundOutline(Ui.R(44, y, 302, 150), 26, 3, Ui.Hex(0x935a2c), Ui.Hex(0x7a4a25));

            int home = Zoo.UnlockedCount();
            for (int i = 0; i < 4; i++)
            {
                float cx = 64 + i * 74;
                var slot = Ui.R(cx, y + 26, 60, 52);
                if (i < home)
                {
                    Ui.RoundGrad(slot, 12, Ui.VGrad(20 + i, (0f, Ui.Hex(0xffdca6)), (1f, Ui.Hex(0xf2a95c))));
                    var art = Zoo.Art(i);
                    if (art != null)
                    {
                        float bob = Mathf.Sin(Time.time * 1.7f + i * 1.3f) * 2.5f;
                        float aw = 38f, ah = aw * art.height / (float)art.width;
                        GUI.DrawTexture(Ui.R(cx + (60 - aw) * 0.5f, y + 26 + (52 - ah) * 0.5f + bob, aw, ah), art);
                    }
                }
                else Ui.RoundOutline(slot, 12, 2, Ui.Hex(0x8a5228), Ui.Hex(0x6b3f20));
            }

            GUI.Label(Ui.R(44, y + 150 - 24, 302, 16),
                      $"the dorm  ·  {home} of {Zoo.Count} home",
                      Ui.Bold(12, new Color(1f, 0.91f, 0.776f, 0.72f)));
        }

        /// The continue button. It is the only chunky terracotta thing on the screen,
        /// and it names where you are going rather than just saying "Play" — so the
        /// main action carries information instead of being a shape you memorise.
        private bool ContinueButton(float x, float y, float w, float h, int lv, int ch)
        {
            var outer = Ui.R(x, y, w, h);
            bool down = outer.Contains(Event.current.mousePosition) && Input.GetMouseButton(0);
            float lift = down ? Ui.P(2f) : Ui.P(4f);
            float edge = Ui.P(7f);

            Ui.Round(new Rect(outer.x, outer.y + Ui.P(11f), outer.width, outer.height), 999,
                     new Color(Ui.PrimaryShadow.r, Ui.PrimaryShadow.g, Ui.PrimaryShadow.b, 0.40f));
            Ui.Round(new Rect(outer.x, outer.y, outer.width, outer.height + edge), 999, Ui.PrimaryBase);
            var face = new Rect(outer.x, outer.y - lift + edge, outer.width, outer.height);
            Ui.RoundGrad(face, 999, Ui.VGrad(1, (0f, Ui.PrimaryTop), (1f, Ui.PrimaryBot)));

            float fy = y - lift / Ui.S + 7f;
            GUI.Label(Ui.R(x + 24, fy + 12, 160, 13), Ui.Track("continue"),
                      Ui.Bold(11, new Color(1f, 0.941f, 0.871f, 0.72f), TextAnchor.MiddleLeft));
            GUI.Label(Ui.R(x + 24, fy + 28, 200, 30), $"Level {lv + 1}",
                      Ui.Head(27, Ui.PrimaryInk, TextAnchor.MiddleLeft));

            GUI.Label(Ui.R(x + w - 120 - 58, fy + 14, 120, 13), SleepyZoo.PuzzleGame.RoomName(ch),
                      Ui.Bold(11, new Color(1f, 0.941f, 0.871f, 0.72f), TextAnchor.MiddleRight));
            int got = SleepyZoo.PuzzleGame.StarsFor(lv);
            for (int i = 0; i < 3; i++)
                Ui.StarShape(Ui.R(x + w - 58 - 47 + i * 14, fy + 32, 11, 11),
                             i < got ? Ui.StarLit : new Color(1, 1, 1, 0.35f));

            var disc = Ui.R(x + w - 58, fy + h * 0.5f - 23, 46, 46);
            Ui.Round(disc, 23, new Color(1f, 0.965f, 0.918f, 0.16f));
            var pc = GUI.color; GUI.color = Ui.PrimaryInk;
            GUI.DrawTexture(Ui.R(x + w - 58 + 12, fy + h * 0.5f - 12, 22, 22), Icons.Play);
            GUI.color = pc;

            return GUI.Button(new Rect(outer.x, outer.y, outer.width, outer.height + edge),
                              GUIContent.none, GUIStyle.none);
        }

        /// The permanent bottom rail. Three tertiary destinations, all the same
        /// weight, none of them competing with the primary above them.
        private void DrawNav(float y)
        {
            float w = (Ui.W - 44 - 20) / 3f;
            if (NavItem(22, y, w, 62, Icons.Map, "Map", null, default, default))
            { Sfx.Tap(); _screen = Screen2.Map; _scrollToCurrent = true; }

            if (NavItem(22 + w + 10, y, w, 62, Icons.Bed, "Dorm",
                        $"{Zoo.UnlockedCount()}/{Zoo.Count}", Ui.Hex(0xc67139), Color.white))
            { Sfx.Tap(); _screen = Screen2.Dorm; _sel = -1; }

            bool night = Nightly.Available;
            if (NavItem(22 + (w + 10) * 2, y, w, 62, Icons.Moon, "Tonight",
                        night && Nightly.Streak > 0 ? Nightly.Streak.ToString() : null,
                        Ui.Hex(0xffd166), Ui.Umber) && night)
            {
                Sfx.Tap();
                PlayerPrefs.SetInt(SleepyZoo.PuzzleGame.DailyRequestKey, 1);
                PlayerPrefs.Save();
                SceneManager.LoadScene("Puzzle");
            }
        }

        private bool NavItem(float x, float y, float w, float h, Texture2D icon, string label,
                             string badge, Color badgeBg, Color badgeInk)
        {
            var r = Ui.R(x, y, w, h);
            bool down = r.Contains(Event.current.mousePosition) && Input.GetMouseButton(0);
            Ui.RoundOutline(r, 22, 1.5f, Ui.Hex(0xffe1be, down ? 0.5f : 0.3f),
                            new Color(0.251f, 0.137f, 0.063f, down ? 0.36f : 0.24f));
            var pc = GUI.color; GUI.color = Ui.Ghost(0.94f);
            GUI.DrawTexture(Ui.R(x + w * 0.5f - 10, y + 12, 20, 20), icon);
            GUI.color = pc;
            GUI.Label(Ui.R(x, y + 34, w, 18), label, Ui.Bold(12, Ui.Ghost(0.94f)));

            if (!string.IsNullOrEmpty(badge))
            {
                var st = Ui.Bold(10, badgeInk);
                float bw = st.CalcSize(new GUIContent(badge)).x / Ui.S + 14f;
                var br = Ui.R(x + w - bw + 4, y - 6, bw, 18);
                Ui.Round(new Rect(br.x - Ui.P(2), br.y - Ui.P(2), br.width + Ui.P(4), br.height + Ui.P(4)),
                         999, Ui.Hex(0x4a2a18));
                Ui.Round(br, 999, badgeBg);
                GUI.Label(br, badge, st);
            }
            return GUI.Button(r, GUIContent.none, GUIStyle.none);
        }

        // =====================================================================
        // 1b — THE MAP
        // A quilt you scroll, chapters as bands.
        // =====================================================================
        // Each chapter gets the colours of its own room, so scrolling the map is
        // the same journey as playing it. A locked band goes dark and keeps its
        // secret.
        private static readonly uint[,] BandCols =
        {
            { 0xf3e6e9, 0xf7efe7, 0x7c4f5e }, { 0xe4eddd, 0xf0f2e7, 0x4d6b46 },
            { 0xdfeaf2, 0xeef1e6, 0x3a5a72 }, { 0xe6edf5, 0xf2f5f9, 0x46607c },
            { 0xf6e7d8, 0xf8f0e4, 0x8a5730 }, { 0xe0efe6, 0xeef4ec, 0x3f6b55 },
            { 0xece4f2, 0xf3eff6, 0x5b4a75 }, { 0xe3e3f2, 0xeeeef7, 0x3b3b6b },
        };

        private const float NodeStep = 92f, NodeW = 64f, NodeH = 60f, BandHead = 74f, BandPad = 26f;

        private float BandHeight(int ch)
        {
            if (!SleepyZoo.PuzzleGame.ChapterUnlocked(ch)) return 150f;
            int n = SleepyZoo.PuzzleGame.ChapterLastLevel(ch) - SleepyZoo.PuzzleGame.ChapterFirstLevel(ch) + 1;
            return BandHead + (n - 1) * NodeStep + NodeH + BandPad;
        }

        private void DrawMap()
        {
            float H = Ui.H;
            Ui.Backdrop(11, (0f, Ui.Cream), (1f, Ui.Hex(0xe9dcc6)));

            int chapters = SleepyZoo.PuzzleGame.ChapterCount;
            float contentH = 0f;
            for (int ch = 0; ch < chapters; ch++) contentH += BandHeight(ch);

            var view = Ui.R(0, 0, Ui.W, H);
            _scrollView = view;
            _scrollMax = Mathf.Max(0f, contentH - (H - 118f));

            if (_scrollToCurrent)
            {
                _scrollToCurrent = false;
                int here = SleepyZoo.PuzzleGame.ResumeLevel();
                int hereCh = SleepyZoo.PuzzleGame.ChapterOf(here);
                float upto = 0f;
                for (int ch = 0; ch < hereCh; ch++) upto += BandHeight(ch);
                int into = here - SleepyZoo.PuzzleGame.ChapterFirstLevel(hereCh);
                _mapScroll = Mathf.Clamp(upto + BandHead + into * NodeStep - H * 0.45f, 0f, _scrollMax);
            }

            // ---- the bands ----
            GUI.BeginGroup(view);
            float y = 118f - _mapScroll;
            for (int ch = 0; ch < chapters; ch++)
            {
                float bh = BandHeight(ch);
                if (y + bh > -40f && y < H + 40f)
                {
                    if (SleepyZoo.PuzzleGame.ChapterUnlocked(ch)) DrawOpenBand(y, bh, ch);
                    else DrawLockedBand(y, bh, ch);
                }
                y += bh;
            }
            GUI.EndGroup();

            // ---- the header, floating over a fade ----
            GUI.DrawTexture(Ui.R(0, 0, Ui.W, 118),
                Ui.VGrad(12, (0f, Ui.Cream), (0.62f, Ui.Cream), (1f, new Color(0.961f, 0.918f, 0.847f, 0f))),
                ScaleMode.StretchToFill);

            if (Ui.GhostDisc(20, 52, 40, Icons.Chevron, 0.45f, Ui.Ink800, false))
            { Sfx.Click(); _screen = Screen2.Home; }
            Ui.Round(Ui.R(20, 52, 40, 40), 20, Ui.Hex(0xe9e2d4, 0f));   // keep the disc reading on cream
            GUI.Label(Ui.R(72, 54, 200, 22), "The map", Ui.Head(20, Ui.Ink900, TextAnchor.MiddleLeft));
            GUI.Label(Ui.R(72, 74, 240, 16),
                      $"{SleepyZoo.PuzzleGame.ChapterCount} chapters · {SleepyZoo.PuzzleGame.LevelCount} nights",
                      Ui.Bold(11, Ui.Ink600, TextAnchor.MiddleLeft));
            Ui.Chip(Ui.W - 100, 56, 32, SleepyZoo.PuzzleGame.TotalStars().ToString(),
                    Ui.StarTex, Ui.Star, Ui.Ink900, Ui.Hex(0xe9e2d4), 14f);

            DrawGateHelp();
        }

        private void DrawOpenBand(float y, float bh, int ch)
        {
            var band = Ui.R(0, y, Ui.W, bh);
            GUI.DrawTexture(band, Ui.VGrad(30 + ch,
                (0f, Ui.Hex(BandCols[ch, 0])), (1f, Ui.Hex(BandCols[ch, 1]))), ScaleMode.StretchToFill);

            int first = SleepyZoo.PuzzleGame.ChapterFirstLevel(ch);
            int last = SleepyZoo.PuzzleGame.ChapterLastLevel(ch);
            int n = last - first + 1;
            int done = 0;
            for (int i = first; i <= last; i++) if (SleepyZoo.PuzzleGame.StarsFor(i) > 0) done++;

            // header row
            Ui.Round(Ui.R(24, y + 20, 46, 46), 16, Ui.Hex(BandCols[ch, 2]));
            GUI.Label(Ui.R(24, y + 20, 46, 46), (ch + 1).ToString(), Ui.Head(20, Ui.Hex(BandCols[ch, 1])));
            GUI.Label(Ui.R(82, y + 22, Ui.W - 100, 22), SleepyZoo.PuzzleGame.ChapterName(ch),
                      Ui.Head(19, Ui.Ink900, TextAnchor.MiddleLeft));
            GUI.Label(Ui.R(82, y + 44, Ui.W - 100, 18),
                      $"{SleepyZoo.PuzzleGame.RoomName(ch)} · {done} of {n} tucked in",
                      Ui.Bold(11.5f, Ui.Ink700, TextAnchor.MiddleLeft));

            // the winding path
            float top = y + BandHead + NodeH * 0.5f;
            float cx = Ui.W * 0.5f;
            float amp = 96f;
            for (int i = first; i < last; i++)
            {
                int k = i - first;
                var a = new Vector2(cx + PathX(k, amp), top + k * NodeStep);
                var b = new Vector2(cx + PathX(k + 1, amp), top + (k + 1) * NodeStep);
                DrawStitches(a, b, SleepyZoo.PuzzleGame.StarsFor(i) > 0);
            }
            for (int i = first; i <= last; i++)
            {
                int k = i - first;
                DrawNode(cx + PathX(k, amp), top + k * NodeStep, i);
            }
        }

        // The path's sideways wander. Two sines so it never looks like a plain zigzag.
        private static float PathX(int k, float amp) =>
            Mathf.Sin(k * 0.85f) * amp * 0.80f + Mathf.Sin(k * 0.37f) * amp * 0.20f;

        /// The dotted stitching between two nodes — sparse, like the design's
        /// `stroke-dasharray: 2 13`. Behind you it is terracotta; ahead it is grey.
        private void DrawStitches(Vector2 a, Vector2 b, bool done)
        {
            const int dots = 6;
            var c = done ? Ui.Hex(0xc67139) : Ui.Hex(0xc0b6a5);
            for (int i = 1; i <= dots; i++)
            {
                var p = Vector2.Lerp(a, b, i / (float)(dots + 1));
                Ui.Circle(Ui.R(p.x - 2, p.y - 2, 4, 4), c);
            }
        }

        private void DrawNode(float cx, float cy, int i)
        {
            bool open = SleepyZoo.PuzzleGame.IsUnlocked(i);
            int stars = SleepyZoo.PuzzleGame.StarsFor(i);
            bool next = open && stars == 0;

            if (next)
            {
                // the one you are up to: bigger, terracotta, with a breathing halo
                float w = 78, h = 74;
                float pulse = 8f + 4f * Mathf.Sin(Time.time * 2.4f);
                Ui.RoundOutline(Ui.R(cx - w * 0.5f - pulse, cy - h * 0.5f - pulse, w + pulse * 2, h + pulse * 2),
                                34, 2, Ui.Hex(0xc67139, 0.35f), new Color(0, 0, 0, 0f));
                var r = Ui.R(cx - w * 0.5f, cy - h * 0.5f, w, h);
                Ui.Round(new Rect(r.x, r.y + Ui.P(5), r.width, r.height), 22, Ui.Hex(0x8c491a));
                Ui.RoundGrad(r, 22, Ui.VGrad(1, (0f, Ui.PrimaryTop), (1f, Ui.PrimaryBot)));
                GUI.Label(Ui.R(cx - w * 0.5f, cy - h * 0.5f + 4, w, 40), (i + 1).ToString(),
                          Ui.Head(28, Ui.PrimaryInk));
                GUI.Label(Ui.R(cx - w * 0.5f, cy + h * 0.5f - 20, w, 14), Ui.Track("next"),
                          Ui.Bold(10, new Color(1f, 0.965f, 0.918f, 0.8f)));

                // and the one line of information that matters before you tap it
                string tip = $"{SleepyZoo.PuzzleGame.AnimalCount(i)} animals · par {SleepyZoo.PuzzleGame.ParOf(i)}";
                var st = Ui.Bold(11, Ui.Hex(0xffeed6));
                float tw = st.CalcSize(new GUIContent(tip)).x / Ui.S + 24f;
                float tx = Mathf.Clamp(cx + 46, 8, Ui.W - tw - 8);
                Ui.Round(Ui.R(tx, cy + 26, tw, 26), 12, Ui.Umber);
                GUI.Label(Ui.R(tx, cy + 26, tw, 26), tip, st);

                if (GUI.Button(r, GUIContent.none, GUIStyle.none) && !Dragged) Launch(i);
                return;
            }

            var nr = Ui.R(cx - NodeW * 0.5f, cy - NodeH * 0.5f, NodeW, NodeH);
            if (open)
            {
                Ui.Round(new Rect(nr.x, nr.y + Ui.P(3), nr.width, nr.height), 18, Ui.Line);
                Ui.RoundOutline(nr, 18, 2, Ui.Line, Ui.CreamTile);
                GUI.Label(Ui.R(cx - NodeW * 0.5f, cy - NodeH * 0.5f + 4, NodeW, 30), (i + 1).ToString(),
                          Ui.Head(22, Ui.Ink800));
                for (int s = 0; s < 3; s++)
                    Ui.StarShape(Ui.R(cx - 16 + s * 12, cy + NodeH * 0.5f - 17, 10, 10),
                                 s < stars ? Ui.Star : Ui.Line);
                if (GUI.Button(nr, GUIContent.none, GUIStyle.none) && !Dragged) Launch(i);
            }
            else
            {
                Ui.RoundOutline(nr, 18, 2, Ui.Line, Ui.LockedFill);
                var pc = GUI.color; GUI.color = Ui.LockedInk;
                GUI.DrawTexture(Ui.R(cx - 10, cy - 10, 20, 20), Icons.Lock);
                GUI.color = pc;
                // a star checkpoint explains itself rather than just saying no
                if (SleepyZoo.PuzzleGame.StarsFor(i - 1) > 0 &&
                    GUI.Button(nr, GUIContent.none, GUIStyle.none) && !Dragged)
                {
                    Sfx.Locked();
                    _gateLevel = i;
                    _topUpLevel = SleepyZoo.PuzzleGame.EasiestTopUpLevel();
                    _gateTime = 6f;
                }
            }
        }

        private void Launch(int level)
        {
            Sfx.Tap();
            PlayerPrefs.SetInt("zoo_level", level);
            PlayerPrefs.Save();
            SceneManager.LoadScene("Puzzle");
        }

        /// A locked chapter: dark, named only as "? ? ?", carrying its tease, the
        /// exact star price, and somewhere to go and earn it. Knowing that SOMETHING
        /// changes without knowing what is the whole reason the last few levels of
        /// the chapter before are worth three-starring.
        private void DrawLockedBand(float y, float bh, int ch)
        {
            Ui.Fill(Ui.R(0, y, Ui.W, bh), Ui.PanelDark);
            var ink = Ui.Hex(0xf0e6d4);

            Ui.Round(Ui.R(24, y + 26, 46, 46), 16, new Color(ink.r, ink.g, ink.b, 0.12f));
            GUI.Label(Ui.R(24, y + 26, 46, 46), (ch + 1).ToString(), Ui.Head(20, ink));
            GUI.Label(Ui.R(82, y + 28, Ui.W - 100, 22), "? ? ?", Ui.Head(19, ink, TextAnchor.MiddleLeft));
            GUI.Label(Ui.R(82, y + 50, Ui.W - 106, 18), SleepyZoo.PuzzleGame.ChapterTease(ch),
                      Ui.Bold(11.5f, new Color(ink.r, ink.g, ink.b, 0.62f), TextAnchor.MiddleLeft));

            // A chapter has TWO doors: enough stars, and having finished the chapter
            // before it. Showing the star bar when the stars are already paid read as
            // a bug ("145 / 126" on a full bar, still locked), so once the stars are
            // in, the band stops talking about stars and names the real blocker.
            int need = SleepyZoo.PuzzleGame.ChapterRequiredStars(ch);
            int have = SleepyZoo.PuzzleGame.TotalStars();
            bool starsPaid = have >= need;
            int gap = Mathf.Max(0, need - have);

            Ui.Bar(24, y + 92, Ui.W - 48 - 74, 12, need <= 0 ? 1f : Mathf.Clamp01(have / (float)need),
                   new Color(ink.r, ink.g, ink.b, 0.14f), Ui.Hex(0xe08b46), Ui.Hex(0xffd166));
            GUI.Label(Ui.R(Ui.W - 24 - 70, y + 88, 70, 20),
                      starsPaid ? "stars paid" : $"{have} / {need} ★",
                      Ui.Bold(starsPaid ? 11 : 12, Ui.Hex(0xffd166), TextAnchor.MiddleRight));

            GUI.Label(Ui.R(24, y + 114, Ui.W - 48, 18),
                      starsPaid
                        ? $"Finish chapter {ch} to open this one."
                        : $"{gap} stars away. Replay level {SleepyZoo.PuzzleGame.EasiestTopUpLevel() + 1} for an easy three.",
                      Ui.Bold(12, new Color(ink.r, ink.g, ink.b, 0.7f), TextAnchor.MiddleLeft));
        }

        /// Tapping a star-locked level used to be a dead end. Now it says the price,
        /// the gap, and offers the easiest level to replay for the difference.
        private void DrawGateHelp()
        {
            if (_gateTime <= 0f) return;
            _gateTime -= Time.deltaTime;
            float h = 84f;
            var r = Ui.R(16, Ui.H - h - 16, Ui.W - 32, h);
            Ui.Round(r, 22, Ui.Hex(0x2a1f33, 0.96f));

            int need = SleepyZoo.PuzzleGame.RequiredStars(_gateLevel);
            int gap = need - SleepyZoo.PuzzleGame.TotalStars();
            GUI.Label(Ui.R(32, Ui.H - h - 4, Ui.W - 190, 22),
                      $"Level {_gateLevel + 1} opens at {need} stars",
                      Ui.Bold(13, Ui.Hex(0xffd166), TextAnchor.MiddleLeft));
            GUI.Label(Ui.R(32, Ui.H - h + 20, Ui.W - 190, 22),
                      gap == 1 ? "1 star to go" : $"{gap} stars to go",
                      Ui.Bold(12, Ui.Ghost(0.7f), TextAnchor.MiddleLeft));
            if (Ui.Outline(Ui.W - 148, Ui.H - h + 8, 132, 44, $"Replay {_topUpLevel + 1}", 13))
            { Launch(_topUpLevel); }
        }

        // =====================================================================
        // 1c — THE DORM
        // Tap an animal. Feed, pet, tuck in.
        // =====================================================================
        // Where each friend sleeps. Fixed anchors (they drift a little around them)
        // so the room has a shape you learn, rather than animals sliding around
        // under your thumb while you try to tap one.
        private static readonly float[,] Spots =
        {
            { 14, 6 }, { 140, 96 }, { 258, 14 }, { 22, 212 }, { 210, 220 },
            { 128, 306 }, { 262, 130 }, { 16, 116 }, { 250, 300 }, { 128, 196 },
        };

        private void DrawDorm()
        {
            float H = Ui.H;
            Ui.Backdrop(13, (0f, Ui.DormTop), (0.46f, Ui.DormMid), (1f, Ui.DormBot));

            // lamplight: the one thing Decorate changes
            var lamp = Dorm.ThemeColor;
            Ui.Glow(Ui.R(60, 150, Ui.W - 120, 150), 60f, new Color(lamp.r, lamp.g, lamp.b, 0.20f));

            // ---- header ----
            if (Ui.GhostDisc(18, 58, 40, Icons.Chevron, 0.45f))
            { Sfx.Click(); _screen = Screen2.Home; _sel = -1; }
            GUI.Label(Ui.R(70, 58, 220, 22), "The dorm", Ui.Head(20, Ui.Hex(0xfff4e4), TextAnchor.MiddleLeft));
            GUI.Label(Ui.R(70, 78, 240, 16),
                      $"{Zoo.UnlockedCount()} friends home · {Nightly.Lanterns} lanterns lit",
                      Ui.Bold(11, new Color(1f, 0.925f, 0.816f, 0.6f), TextAnchor.MiddleLeft));
            Ui.Chip(Ui.W - 92, 58, 32, Dorm.Snacks.ToString(), Icons.Snack, Ui.Snack,
                    Ui.Hex(0xffe9bd), Ui.Hex(0xffd166, 0.16f), 14f);

            // two lanterns on the wall, breathing
            for (int i = 0; i < 2; i++)
            {
                float x = i == 0 ? 26 : Ui.W - 48;
                float b = 0.28f + 0.12f * Mathf.Sin(Time.time * 1.4f + i * 2f);
                Ui.Glow(Ui.R(x, 126, 22, 30), 22f, new Color(lamp.r, lamp.g, lamp.b, b));
                Ui.Round(Ui.R(x, 126, 22, 30), 8, Ui.Hex(0xffcf8d));
            }

            // ---- the friends ----
            float roomTop = 172f, roomBot = H - 150f;
            for (int i = 0; i < Zoo.Count; i++)
            {
                if (!Zoo.Unlocked(i)) continue;
                float ax = Spots[i, 0], ay = Spots[i, 1];
                // keep everyone inside the room however tall the phone is
                ay = Mathf.Min(ay, Mathf.Max(0f, roomBot - roomTop - 110f));
                DrawPal(i, ax, roomTop + ay);
            }

            DrawHeartFlourish();

            // a gentle fade so names never fight the buttons underneath
            GUI.DrawTexture(Ui.R(0, H - 168, Ui.W, 64),
                Ui.VGrad(14, (0f, new Color(0.141f, 0.094f, 0.075f, 0f)),
                             (1f, new Color(0.141f, 0.094f, 0.075f, 0.9f))), ScaleMode.StretchToFill);

            if (_sel >= 0) DrawFriendSheet();
            else
            {
                GUI.Label(Ui.R(20, H - 104, Ui.W - 40, 18), Dorm.Hint(),
                          Ui.Bold(12, new Color(1f, 0.925f, 0.816f, 0.62f)));
                float bw = (Ui.W - 40 - 11) / 2f;
                if (Ui.Primary(20, H - 80, bw, 52, "Tuck everyone in", 16f))
                { Sfx.Sleep(); Dorm.TuckEveryone(); }
                if (Ui.Outline(20 + bw + 11, H - 80, bw, 52, "Decorate", 18f, 999f, true))
                { Sfx.Tap(); _screen = Screen2.Decorate; }
            }
        }

        private void DrawPal(int i, float x, float y)
        {
            var pal = Zoo.Pals[i];
            bool asleep = Dorm.Asleep(i);
            // asleep animals breathe; awake ones bob. Both are slow — this is a
            // bedroom, not an idle-animation showreel.
            float t = Time.time * (asleep ? 0.6f : 0.9f) + i * 1.4f;
            float bob = asleep ? 0f : Mathf.Sin(t) * 3f;
            float breathe = asleep ? 1f + Mathf.Sin(t) * 0.03f : 1f;

            // the bed: a coloured back and a cream turned-down sheet
            Ui.Round(Ui.R(x + 6, y + 30, 100, 44), 16, pal.col);
            Ui.Round(Ui.R(x + 6, y + 52, 100, 22), 12, new Color(1f, 0.973f, 0.925f, 0.9f));

            var art = Zoo.Art(i);
            if (art != null)
            {
                float aw = 60f * breathe, ah = aw * art.height / (float)art.width;
                GUI.DrawTexture(Ui.R(x + 56 - aw * 0.5f, y + 58 - ah + bob, aw, ah), art);
            }

            if (asleep)
                GUI.Label(Ui.R(x + 70, y + 8, 40, 24), "z",
                          Ui.Bold(22, new Color(1f, 0.925f, 0.816f, 0.5f + 0.2f * Mathf.Sin(t))));

            GUI.Label(Ui.R(x - 2, y + 78, 118, 20), pal.name, Ui.Head(15, Ui.Hex(0xfff4e4)));
            GUI.Label(Ui.R(x - 2, y + 96, 118, 16), Dorm.MoodWord(i), Ui.Bold(10.5f, Dorm.MoodColor(i)));

            if (GUI.Button(Ui.R(x, y, 118, 112), GUIContent.none, GUIStyle.none))
            { Sfx.Tap(); _sel = i; }
        }

        /// The bottom sheet for one friend: who they are, what they are dreaming
        /// about, and the three things you can do. Feed costs a snack, Pet is always
        /// free — so an empty jar never means there is nothing to do here.
        private void DrawFriendSheet()
        {
            float H = Ui.H;
            int i = _sel;
            var pal = Zoo.Pals[i];
            float sh = 250f;
            float y = H - sh;

            Ui.Round(Ui.R(0, y, Ui.W, sh + 40), 30, Ui.Cream);
            Ui.Round(Ui.R(Ui.W * 0.5f - 21, y + 12, 42, 5), 3, Ui.Line);

            Ui.Round(Ui.R(22, y + 28, 74, 74), 24, pal.col);
            var art = Zoo.Art(i);
            if (art != null)
            {
                float aw = 54f, ah = aw * art.height / (float)art.width;
                GUI.DrawTexture(Ui.R(22 + (74 - aw) * 0.5f, y + 28 + (74 - ah) * 0.5f, aw, ah), art);
            }

            GUI.Label(Ui.R(110, y + 32, 200, 28), pal.name, Ui.Head(26, Ui.Ink900, TextAnchor.MiddleLeft));
            GUI.Label(Ui.R(110, y + 60, Ui.W - 130, 18),
                      char.ToUpper(pal.dream[0]) + pal.dream.Substring(1) + ".",
                      Ui.Bold(12.5f, Ui.Ink700, TextAnchor.MiddleLeft, true));

            // two quiet tags: how settled they are, and how they feel
            Tag(110, y + 82, Zoo.StageWord(Zoo.Stage(i)), Ui.Hex(0xf6dcc4), Ui.Hex(0x8a5730));
            Tag(110 + TagW(Zoo.StageWord(Zoo.Stage(i))) + 6, y + 82, Dorm.MoodWord(i),
                Ui.Hex(0xdfeaf2), Ui.Hex(0x3a5a72));

            // the three actions
            float bw = (Ui.W - 44 - 20) / 3f;
            bool canFeed = Dorm.Snacks > 0;
            if (ActionTile(22, y + 118, bw, 76, Icons.Snack, "Feed", "1 snack",
                           Ui.Hex(0xf6dcc4), Ui.Hex(0xe9c39b), Ui.Hex(0x8a5730), canFeed))
            {
                if (Dorm.Feed(i)) { Sfx.Tap(); Flourish(i, Icons.Snack, Ui.Hex(0xd67f48)); }
                else Sfx.Locked();
            }
            if (ActionTile(22 + bw + 10, y + 118, bw, 76, Icons.Heart, "Pet", "free",
                           Ui.Hex(0xffdfe7), Ui.Hex(0xf7c2ce), Ui.Hex(0xa8425c), true))
            { Sfx.Tap(); Dorm.Pet(i); Flourish(i, Icons.Heart, Ui.Hex(0xee7c9b)); }
            if (ActionTile(22 + (bw + 10) * 2, y + 118, bw, 76, Icons.Bed, "Tuck in", "goodnight",
                           Ui.Hex(0xe9e2d4), Ui.Line, Ui.Ink800, true))
            { Sfx.Sleep(); Dorm.Tuck(i); _sel = -1; }

            if (GUI.Button(Ui.R(0, y + 202, Ui.W, 30), "Close", Ui.Bold(13, Ui.Ink600)))
            { Sfx.Click(); _sel = -1; }
        }

        private float TagW(string s) => Ui.Bold(10.5f, Color.white).CalcSize(new GUIContent(s)).x / Ui.S + 20f;
        private void Tag(float x, float y, string s, Color bg, Color ink)
        {
            float w = TagW(s);
            Ui.Round(Ui.R(x, y, w, 20), 999, bg);
            GUI.Label(Ui.R(x, y, w, 20), s, Ui.Bold(10.5f, ink));
        }

        private bool ActionTile(float x, float y, float w, float h, Texture2D icon, string label,
                                string sub, Color bg, Color border, Color ink, bool enabled)
        {
            var r = Ui.R(x, y, w, h);
            float a = enabled ? 1f : 0.45f;
            Ui.RoundOutline(r, 22, 2, new Color(border.r, border.g, border.b, a),
                            new Color(bg.r, bg.g, bg.b, a));
            var pc = GUI.color; GUI.color = new Color(ink.r, ink.g, ink.b, a);
            GUI.DrawTexture(Ui.R(x + w * 0.5f - 12.5f, y + 14, 25, 25), icon);
            GUI.color = pc;
            GUI.Label(Ui.R(x, y + 42, w, 16), label, Ui.Bold(12, new Color(ink.r, ink.g, ink.b, a)));
            GUI.Label(Ui.R(x, y + 56, w, 14), sub, Ui.Bold(10, new Color(ink.r, ink.g, ink.b, a * 0.7f)));
            return GUI.Button(r, GUIContent.none, GUIStyle.none);
        }

        /// A snack or a heart floating up off whoever you just touched. One second,
        /// then gone — the smallest possible "that did something".
        private void Flourish(int i, Texture2D icon, Color col)
        {
            _heartAt = Time.time;
            _heartPos = new Vector2(Spots[i, 0] + 76, 172f + Spots[i, 1] + 10);
            _heartCol = col;
            _heartIcon = icon;
        }

        private void DrawHeartFlourish()
        {
            if (_heartIcon == null) return;
            float t = (Time.time - _heartAt) / 1f;
            if (t >= 1f) { _heartIcon = null; return; }
            var pc = GUI.color;
            GUI.color = new Color(_heartCol.r, _heartCol.g, _heartCol.b, 1f - t);
            GUI.DrawTexture(Ui.R(_heartPos.x, _heartPos.y - t * 40f, 30, 30), _heartIcon);
            GUI.color = pc;
        }

        // =====================================================================
        // DECORATE
        // =====================================================================
        // The one screen the redesign names but never draws. Kept deliberately
        // small and made of colours the game already owns, so it cannot introduce
        // art that fights the rest of the room.
        private void DrawDecorate()
        {
            float H = Ui.H;
            Ui.Backdrop(13, (0f, Ui.DormTop), (0.46f, Ui.DormMid), (1f, Ui.DormBot));
            var lamp = Dorm.ThemeColor;
            Ui.Glow(Ui.R(60, 150, Ui.W - 120, 200), 70f, new Color(lamp.r, lamp.g, lamp.b, 0.22f));

            if (Ui.GhostDisc(18, 58, 40, Icons.Chevron, 0.45f))
            { Sfx.Click(); _screen = Screen2.Dorm; }
            GUI.Label(Ui.R(70, 58, 240, 22), "Decorate", Ui.Head(20, Ui.Hex(0xfff4e4), TextAnchor.MiddleLeft));
            GUI.Label(Ui.R(70, 78, 260, 16), "Choose the lamplight.",
                      Ui.Bold(11, new Color(1f, 0.925f, 0.816f, 0.6f), TextAnchor.MiddleLeft));

            for (int i = 0; i < Dorm.ThemeNames.Length; i++)
            {
                float y = 150 + i * 78;
                bool on = Dorm.Theme == i;
                var r = Ui.R(24, y, Ui.W - 48, 64);
                Ui.RoundOutline(r, 22, 2, on ? Ui.Hex(0xffd166, 0.8f) : Ui.Ghost(0.2f),
                                Ui.Ghost(on ? 0.12f : 0.06f));
                var c = i == Dorm.Theme ? lamp : ThemeSwatch(i);
                Ui.Glow(Ui.R(46, y + 18, 28, 28), 18f, new Color(c.r, c.g, c.b, 0.4f));
                Ui.Circle(Ui.R(46, y + 18, 28, 28), c);
                GUI.Label(Ui.R(94, y, 200, 64), Dorm.ThemeNames[i],
                          Ui.Head(18, Ui.Hex(0xfff4e4), TextAnchor.MiddleLeft));
                if (on) GUI.Label(Ui.R(Ui.W - 130, y, 90, 64), "lit now",
                                  Ui.Bold(11, Ui.Hex(0xffd166), TextAnchor.MiddleRight));
                if (GUI.Button(r, GUIContent.none, GUIStyle.none)) { Sfx.Tap(); Dorm.Theme = i; }
            }

            if (Ui.Primary(24, H - 92, Ui.W - 48, 56, "Back to the dorm", 18f))
            { Sfx.Click(); _screen = Screen2.Dorm; }
        }

        private static Color ThemeSwatch(int i)
        {
            int saved = Dorm.Theme;
            Dorm.Theme = i;             // read the palette without a second table
            var c = Dorm.ThemeColor;
            Dorm.Theme = saved;
            return c;
        }

        // =====================================================================
        // SETTINGS
        // =====================================================================
        private void DrawSettings()
        {
            float H = Ui.H;
            Ui.Backdrop(10, (0f, Ui.NightTop), (0.34f, Ui.NightMid), (0.62f, Ui.NightWarm),
                        (0.84f, Ui.NightGlow), (1f, Ui.NightLow));

            if (Ui.GhostDisc(18, 58, 40, Icons.Chevron, 0.45f))
            { Sfx.Click(); _screen = Screen2.Home; }
            GUI.Label(Ui.R(70, 58, 240, 22), "Settings", Ui.Head(20, Ui.Hex(0xfff4e4), TextAnchor.MiddleLeft));

            float y = 140;
            if (Row(y, Sfx.SoundOn ? Icons.Speaker : Icons.SpeakerOff, "Sound", Sfx.SoundOn ? "On" : "Off"))
            { Sfx.SoundOn = !Sfx.SoundOn; Sfx.Click(); }
            y += 76;
            if (Row(y, Icons.Buzz, "Vibration", Haptics.Enabled ? "On" : "Off"))
            {
                Haptics.Enabled = !Haptics.Enabled;
                Sfx.Tap();
                if (Haptics.Enabled) Haptics.Soft();   // let them feel what they turned on
            }
            y += 76;
            if (Row(y, Icons.Share, "Tell a friend", ""))
            { Sfx.Click(); NativeShare.ShareText(ShareMessage); }

            GUI.Label(Ui.R(0, H - 70, Ui.W, 20), $"Tuck In  {Application.version}",
                      Ui.Bold(12, new Color(1f, 0.925f, 0.816f, 0.45f)));
        }

        private bool Row(float y, Texture2D icon, string label, string value)
        {
            var r = Ui.R(24, y, Ui.W - 48, 60);
            Ui.RoundOutline(r, 22, 1.5f, Ui.Ghost(0.26f), Ui.Ghost(0.08f));
            var pc = GUI.color; GUI.color = Ui.Ghost(0.9f);
            GUI.DrawTexture(Ui.R(44, y + 19, 22, 22), icon);
            GUI.color = pc;
            GUI.Label(Ui.R(80, y, 200, 60), label, Ui.Head(18, Ui.Hex(0xfff4e4), TextAnchor.MiddleLeft));
            if (!string.IsNullOrEmpty(value))
                GUI.Label(Ui.R(Ui.W - 110, y, 66, 60), value,
                          Ui.Bold(14, Ui.Hex(0xffd166), TextAnchor.MiddleRight));
            return GUI.Button(r, GUIContent.none, GUIStyle.none);
        }

        // Plain sentences only. The old copy used an em dash and it came through as a
        // stray glyph in the share sheet on a real phone.
        private const string ShareMessage =
            "I'm playing Tuck In, a cozy bedtime puzzle where one swipe slides every "
            + "animal until something stops them. You just tuck them all into bed. "
            + "It's very relaxing.";

        // =====================================================================
        // ARRIVAL — the "someone moved in" moment (shared with the win screen)
        // =====================================================================
        private void DrawArrivalCard()
        {
            int i = Zoo.PendingArrival();
            if (i < 0) return;
            var pal = Zoo.Pals[i];
            float H = Ui.H;

            Ui.Fill(new Rect(0, 0, UnityEngine.Screen.width, UnityEngine.Screen.height),
                    new Color(0, 0, 0, 0.66f));

            float h = 330f, y = (H - h) * 0.5f;
            Ui.Round(Ui.R(24, y, Ui.W - 48, h), 30, Ui.Hex(0x2b1c15));
            Ui.RoundOutline(Ui.R(24, y, Ui.W - 48, h), 30, 1.5f, Ui.Hex(0xffe1be, 0.24f),
                            new Color(0, 0, 0, 0f));

            GUI.Label(Ui.R(24, y + 26, Ui.W - 48, 18), Ui.Track("someone moved in"),
                      Ui.Bold(11, Ui.Hex(0xffd166)));

            float pulse = 8f + 4f * Mathf.Sin(Time.time * 2.4f);
            var tile = Ui.R(Ui.W * 0.5f - 48, y + 62, 96, 96);
            Ui.RoundOutline(new Rect(tile.x - Ui.P(pulse), tile.y - Ui.P(pulse),
                                     tile.width + Ui.P(pulse * 2), tile.height + Ui.P(pulse * 2)),
                            34, 2, Ui.Hex(0xffd166, 0.5f), new Color(0, 0, 0, 0f));
            Ui.RoundGrad(tile, 28, Ui.VGrad(21, (0f, Ui.Hex(0xffdca6)), (1f, Ui.Hex(0xf2a95c))));
            var art = Zoo.Art(i);
            if (art != null)
            {
                float bob = Mathf.Sin(Time.time * 2.2f) * 4f;
                float aw = 70f, ah = aw * art.height / (float)art.width;
                GUI.DrawTexture(Ui.R(Ui.W * 0.5f - aw * 0.5f, y + 62 + (96 - ah) * 0.5f + bob, aw, ah), art);
            }

            GUI.Label(Ui.R(24, y + 176, Ui.W - 48, 30), $"{pal.name} is here",
                      Ui.Head(24, Ui.Hex(0xfff4e4)));
            GUI.Label(Ui.R(44, y + 206, Ui.W - 88, 34),
                      char.ToUpper(pal.dream[0]) + pal.dream.Substring(1) + ".",
                      Ui.Bold(12.5f, new Color(1f, 0.925f, 0.816f, 0.68f), TextAnchor.UpperCenter, true));

            if (Ui.Primary(44, y + h - 82, Ui.W - 88, 54, "Say hello", 20f))
            {
                Sfx.Win();
                Zoo.MarkArrivalSeen();
                _screen = Screen2.Dorm;
                _sel = -1;
            }
        }
    }
}
