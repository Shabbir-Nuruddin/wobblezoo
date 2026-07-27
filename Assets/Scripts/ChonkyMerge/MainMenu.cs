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
        private enum Panel { None, HighScore, Settings, Levels }
        private Panel _panel = Panel.None;

        private SpriteRenderer _soundIcon;
        private readonly System.Collections.Generic.List<Transform> _floaters = new();
        private readonly System.Collections.Generic.List<Vector3> _floaterBase = new();
        private GUIStyle _title, _big, _mid, _btn, _cellNum, _pill, _bigPill, _note, _cellSub, _chapTitle;
        private Texture2D _pillTex, _starTex, _dimTex, _cardTex;
        private Vector2 _levelScroll;
        private float _levelsCenterY, _levelsW, _levelsH;   // world footprint of the big Levels button

        private void Start()
        {
            SetupCamera();
            H = _cam.orthographicSize;
            W = H * _cam.aspect;
            ContentW = Mathf.Min(2f * W, 2f * H * 0.64f); // keep a portrait content column

            BuildBackground();
            BuildFloaters();
            BuildLogo();
            BuildButtons();
            BuildCornerIcons();
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

        private void BuildCornerIcons()
        {
            float m = H * 0.12f;
            // Both corner icons live on the RIGHT so they never sit under the
            // top-left "Levels" tab (which is drawn in OnGUI).
            string snd = Sfx.SoundOn ? "btn_sound_on" : "btn_sound_off";
            var sGo = FitWidth(snd, new Vector2(W - m - ContentW * 0.20f, H - m), 11, ContentW * 0.14f, out _);
            _soundIcon = sGo.GetComponent<SpriteRenderer>();
            var sc = sGo.AddComponent<BoxCollider2D>(); sc.isTrigger = true;
            sGo.AddComponent<MenuButton>().Setup(ButtonId.SoundToggle, sGo.transform.localScale);

            var shGo = FitWidth("btn_share", new Vector2(W - m, H - m), 11, ContentW * 0.14f, out _);
            var shc = shGo.AddComponent<BoxCollider2D>(); shc.isTrigger = true;
            shGo.AddComponent<MenuButton>().Setup(ButtonId.Share, shGo.transform.localScale);
        }

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
                    NativeShare.ShareText("I'm playing Wobble Zoo 🐾 — a cozy bedtime puzzle where you swipe to tuck every sleepy animal into bed. So relaxing!");
                    break;
                case ButtonId.SoundToggle:
                    Sfx.SoundOn = !Sfx.SoundOn;
                    _soundIcon.sprite = Resources.Load<Sprite>("Art/" + (Sfx.SoundOn ? "btn_sound_on" : "btn_sound_off"));
                    break;
            }
        }

        private static int Best() => PlayerPrefs.GetInt("chonky_best", 0);

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
                if (GUI.Button(rLv, "Levels", _bigPill)) { Sfx.Click(); _panel = Panel.Levels; }
                // live star total, tucked just under the button so stars feel worth chasing
                GUI.Label(new Rect(0, rLv.yMax + 2, Screen.width, 30),
                    $"{SleepyZoo.PuzzleGame.TotalStars()} / {SleepyZoo.PuzzleGame.MaxStars} stars collected", _note);
            }

            if (_panel == Panel.Levels) { DrawLevelsPanel(); return; }

            if (_panel == Panel.None) return;

            // dim
            GUI.color = new Color(0, 0, 0, 0.55f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float w = Mathf.Min(Screen.width * 0.82f, 560);
            float h = 360;
            var box = new Rect((Screen.width - w) / 2, (Screen.height - h) / 2, w, h);
            GUI.Box(box, GUIContent.none);

            if (_panel == Panel.HighScore)
            {
                GUI.Label(new Rect(box.x, box.y + 24, box.width, 50), "High Score", _big);
                GUI.Label(new Rect(box.x, box.y + 110, box.width, 90), Best().ToString(), _title);
                if (GUI.Button(new Rect(box.x + box.width / 2 - 150, box.y + 230, 300, 60), "Share my score", _btn))
                { Sfx.Click(); NativeShare.ShareText($"My Wobble Zoo best is {Best()} 🐾 can you beat it?"); }
            }
            else // Settings
            {
                GUI.Label(new Rect(box.x, box.y + 24, box.width, 50), "Settings", _big);
                if (GUI.Button(new Rect(box.x + box.width / 2 - 150, box.y + 110, 300, 60),
                        Sfx.SoundOn ? "Sound:  ON" : "Sound:  OFF", _btn))
                {
                    Sfx.SoundOn = !Sfx.SoundOn; Sfx.Click();
                    if (_soundIcon) _soundIcon.sprite = Resources.Load<Sprite>("Art/" + (Sfx.SoundOn ? "btn_sound_on" : "btn_sound_off"));
                }
                if (GUI.Button(new Rect(box.x + box.width / 2 - 150, box.y + 185, 300, 60), "Reset high score", _btn))
                { Sfx.Click(); PlayerPrefs.SetInt("chonky_best", 0); PlayerPrefs.Save(); }
            }

            if (GUI.Button(new Rect(box.x + box.width - 64, box.y + 12, 48, 48), "X", _btn))
            { Sfx.Click(); _panel = Panel.None; }
        }

        // ---- level picker ----
        // Star economy lives in one place (PuzzleGame) so the menu and the game can
        // never disagree about what's unlocked.
        private static int StarsFor(int i) => SleepyZoo.PuzzleGame.StarsFor(i);
        private static bool Unlocked(int i) => SleepyZoo.PuzzleGame.IsUnlocked(i);
        private static bool GateBlocked(int i) => !Unlocked(i) && StarsFor(i - 1) > 0;

        private void DrawLevelsPanel()
        {
            GUI.color = new Color(0, 0, 0, 0.62f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _dimTex);
            GUI.color = Color.white;

            float w = Mathf.Min(Screen.width * 0.94f, 760);
            float h = Mathf.Min(Screen.height * 0.82f, 1180);
            var box = new Rect((Screen.width - w) / 2, (Screen.height - h) / 2, w, h);
            GUI.Box(box, GUIContent.none);

            GUI.Label(new Rect(box.x, box.y + 18, box.width, 46), "Choose a level", _big);
            GUI.Label(new Rect(box.x, box.y + 62, box.width, 30),
                $"{SleepyZoo.PuzzleGame.TotalStars()} / {SleepyZoo.PuzzleGame.MaxStars} stars collected", _note);
            if (GUI.Button(new Rect(box.xMax - 66, box.y + 14, 50, 50), "X", _pill))
            { Sfx.Click(); _panel = Panel.None; }

            int cols = 4;
            float pad = 24f, gap = 16f;
            float innerW = w - pad * 2;
            float cell = (innerW - gap * (cols - 1)) / cols;
            float rowH = cell + 30f;
            const float headH = 96f, chapGap = 22f;

            int chapters = SleepyZoo.PuzzleGame.ChapterCount;
            float contentH = 0f;
            for (int ch = 0; ch < chapters; ch++)
            {
                int n = SleepyZoo.PuzzleGame.ChapterLastLevel(ch) - SleepyZoo.PuzzleGame.ChapterFirstLevel(ch) + 1;
                contentH += headH + Mathf.CeilToInt(n / (float)cols) * rowH + chapGap;
            }

            var view = new Rect(box.x + pad, box.y + 104, innerW, h - 132);
            var content = new Rect(0, 0, innerW - 16, contentH);
            _levelScroll = GUI.BeginScrollView(view, _levelScroll, content);

            float y = 0f;
            for (int ch = 0; ch < chapters; ch++)
            {
                int first = SleepyZoo.PuzzleGame.ChapterFirstLevel(ch);
                int lastLv = SleepyZoo.PuzzleGame.ChapterLastLevel(ch);
                bool chOpen = SleepyZoo.PuzzleGame.ChapterUnlocked(ch);
                y = DrawChapterHeader(y, innerW - 16, ch, chOpen, headH);

                for (int i = first; i <= lastLv; i++)
                {
                    int k = i - first;
                    int r = k / cols, c = k % cols;
                    var cr = new Rect(c * (cell + gap), y + r * rowH, cell, cell);
                    DrawLevelCell(cr, cell, i, chOpen);
                }
                y += Mathf.CeilToInt((lastLv - first + 1) / (float)cols) * rowH + chapGap;
            }
            GUI.EndScrollView();
        }

        // A chapter strip. A locked chapter deliberately hides its name and its
        // twist — knowing that SOMETHING changes, but not what, is what makes the
        // last few levels of the chapter before it worth grinding stars for.
        private float DrawChapterHeader(float y, float wide, int ch, bool open, float headH)
        {
            var strip = new Rect(0, y, wide, headH - 12f);
            GUI.color = open ? new Color(1f, 1f, 1f, 0.85f) : new Color(0.66f, 0.63f, 0.68f, 0.55f);
            if (_cardTex != null) GUI.DrawTexture(strip, _cardTex);
            else GUI.Box(strip, GUIContent.none);
            GUI.color = Color.white;

            string name = open ? SleepyZoo.PuzzleGame.ChapterName(ch) : "? ? ?";
            GUI.Label(new Rect(strip.x + 14, strip.y + 8, strip.width - 28, 34),
                      $"Chapter {ch + 1}  -  {name}", _chapTitle);

            if (open)
            {
                GUI.Label(new Rect(strip.x + 14, strip.y + 42, strip.width - 28, 34),
                          SleepyZoo.PuzzleGame.ChapterBlurb(ch), _cellSub);
            }
            else
            {
                int need = SleepyZoo.PuzzleGame.ChapterRequiredStars(ch);
                int have = SleepyZoo.PuzzleGame.TotalStars();
                GUI.Label(new Rect(strip.x + 14, strip.y + 40, strip.width - 28, 22),
                          SleepyZoo.PuzzleGame.ChapterTease(ch), _cellSub);
                GUI.Label(new Rect(strip.x + 14, strip.y + 62, strip.width - 28, 22),
                          $"Opens at {need} stars  -  {Mathf.Max(0, need - have)} to go", _cellSub);
            }
            return y + headH;
        }

        private void DrawLevelCell(Rect cr, float cell, int i, bool chapterOpen)
        {
            bool open = chapterOpen && Unlocked(i);
            bool gated = chapterOpen && GateBlocked(i);
            int stars = StarsFor(i);

            GUI.color = open ? Color.white : new Color(0.62f, 0.60f, 0.64f, 0.5f);
            if (_cardTex != null) GUI.DrawTexture(cr, _cardTex);
            else GUI.Box(cr, GUIContent.none);
            GUI.color = Color.white;

            if (open)
            {
                // number + three star pips
                GUI.Label(new Rect(cr.x, cr.y + cell * 0.10f, cr.width, cell * 0.5f),
                          (i + 1).ToString(), _cellNum);
                float ss = cell * 0.26f, sgap = ss * 0.14f, tot = 3 * ss + 2 * sgap;
                float sy = cr.yMax - ss - cell * 0.10f, sx = cr.x + (cell - tot) / 2f;
                for (int s = 0; s < 3; s++)
                {
                    GUI.color = s < stars ? Color.white : new Color(0.35f, 0.30f, 0.34f, 0.5f);
                    if (_starTex != null) GUI.DrawTexture(new Rect(sx + s * (ss + sgap), sy, ss, ss), _starTex);
                }
                GUI.color = Color.white;

                if (GUI.Button(cr, GUIContent.none, GUIStyle.none))
                {
                    Sfx.Click();
                    PlayerPrefs.SetInt("zoo_level", i); PlayerPrefs.Save();
                    SceneManager.LoadScene("Puzzle");
                }
            }
            else if (gated)
            {
                // a star checkpoint: show how many total stars it takes to open
                GUI.Label(new Rect(cr.x, cr.y + cell * 0.06f, cr.width, cell * 0.42f),
                          (i + 1).ToString(), _cellNum);
                float ss = cell * 0.24f;
                var sr = new Rect(cr.x + cell * 0.5f - ss - 2, cr.yMax - ss - cell * 0.12f, ss, ss);
                GUI.color = new Color(1f, 0.86f, 0.30f);
                if (_starTex != null) GUI.DrawTexture(sr, _starTex);
                GUI.color = new Color(0.36f, 0.21f, 0.10f);
                GUI.Label(new Rect(sr.xMax, sr.y - 2, cell * 0.4f, ss + 4),
                          SleepyZoo.PuzzleGame.RequiredStars(i).ToString(), _cellSub);
                GUI.color = Color.white;
            }
            else
            {
                // not reached yet — just a dim number
                GUI.Label(new Rect(cr.x, cr.y + cell * 0.12f, cr.width, cell * 0.5f),
                          (i + 1).ToString(), _cellNum);
            }
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
            _chapTitle = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            _chapTitle.normal.textColor = brown;

            _starTex = Resources.Load<Texture2D>("Art/star_full");
            _cardTex = Resources.Load<Texture2D>("Art/tile_bed");   // warm cream card, matches the puzzle
            _dimTex = Texture2D.whiteTexture;

            var font = Resources.Load<Font>("Fonts/Fredoka");   // cozy rounded font, consistent with the puzzle
            if (font != null) foreach (var st in new[] { _title, _big, _mid, _btn, _pill, _bigPill, _cellNum, _note, _cellSub, _chapTitle }) st.font = font;
        }
    }
}
