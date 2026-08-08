using System.Collections.Generic;
using UnityEngine;

namespace TuckIn
{
    /// <summary>
    /// The design system, in one place.
    ///
    /// Every screen in this game is drawn with IMGUI, which means every screen used
    /// to invent its own sizes out of Screen.width fractions. That is why the old
    /// build had text printing through the logo and buttons that all looked the
    /// same: there was no system, only arithmetic.
    ///
    /// This file is the system. Two ideas hold it together:
    ///
    ///   1. A VIRTUAL CANVAS. The redesign was drawn at 390 x 844 (a phone in CSS
    ///      pixels). Everything below is written in those same numbers, and
    ///      <see cref="Frame"/> scales them onto whatever screen we actually have.
    ///      So a value copied out of the design is the value typed into the code —
    ///      no conversion, no drift, and tall or short phones just get more or less
    ///      room between the top bar and the bottom bar.
    ///
    ///   2. ONE PRIMARY PER SCREEN. <see cref="Primary"/> is the only chunky
    ///      terracotta button with a pressable bottom edge, and it is used exactly
    ///      once per screen. Everything else is an outline pill or a ghost disc, so
    ///      the eye always knows where the main door is.
    ///
    /// Colour rules that the rest of the game must not break:
    ///   Cream is day, deep umber is night. Home and the dorm are lamplit warm;
    ///   the puzzle rooms stay night.
    ///   Stars are gold, snacks are amber. One is progress, one is affection.
    ///   They never mix.
    /// </summary>
    public static class Ui
    {
        // ---- the virtual canvas ----
        public const float DesignW = 390f;

        /// Scale from design units to real pixels, and the left/top origin.
        public static float S { get; private set; } = 1f;
        public static float OX { get; private set; }
        public static float OY { get; private set; }
        /// Usable height in DESIGN units. Taller phones get a bigger number here;
        /// anchor to Top for headers and to <see cref="H"/> for thumb rails.
        public static float H { get; private set; } = 844f;
        public static float W => DesignW;

        /// Call once at the top of OnGUI. Maps the design canvas onto the safe area
        /// so nothing lands under a notch or a gesture bar.
        public static void Frame()
        {
            var sa = Screen.safeArea;
            S = sa.width / DesignW;
            OX = sa.x;
            // Screen.safeArea is bottom-up; IMGUI is top-down.
            OY = Screen.height - (sa.y + sa.height);
            H = sa.height / S;
        }

        /// Design-space rect to screen-space rect.
        public static Rect R(float x, float y, float w, float h) =>
            new Rect(OX + x * S, OY + y * S, w * S, h * S);

        /// A single design-space length in pixels (for font sizes and radii).
        public static float P(float v) => v * S;
        public static int F(float designPx) => Mathf.Max(1, Mathf.RoundToInt(designPx * S));

        // ---- palette ----
        // Straight out of the redesign. Names describe the job, not the hue, so a
        // later art pass can retune one line here instead of hunting hex codes.
        public static Color Hex(uint rgb, float a = 1f) => new Color(
            ((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, a);

        // terracotta — the one primary
        public static readonly Color PrimaryTop = Hex(0xe08b46);
        public static readonly Color PrimaryBot = Hex(0xc67139);
        public static readonly Color PrimaryBase = Hex(0x8c491a);   // the pressable edge
        public static readonly Color PrimaryShadow = Hex(0x5e2f0f);
        public static readonly Color PrimaryInk = Hex(0xfff6ea);

        // night (home, dorm, board, win)
        public static readonly Color NightTop = Hex(0x2a1a14);
        public static readonly Color NightMid = Hex(0x4a2a18);
        public static readonly Color NightWarm = Hex(0x8c491a);
        public static readonly Color NightGlow = Hex(0xc98d4e);
        public static readonly Color NightLow = Hex(0xe6bb84);

        public static readonly Color DormTop = Hex(0x241813);
        public static readonly Color DormMid = Hex(0x3d2619);
        public static readonly Color DormBot = Hex(0x5b3a1e);

        public static readonly Color BoardTop = Hex(0x141024);
        public static readonly Color BoardMid = Hex(0x2a2140);
        public static readonly Color BoardBot = Hex(0x4a3a52);
        public static readonly Color BoardHill = Hex(0x2c2338);

        public static readonly Color WinTop = Hex(0x1d1526);
        public static readonly Color WinMid = Hex(0x3a2440);

        // cream (day)
        public static readonly Color Cream = Hex(0xf5ead8);
        public static readonly Color CreamTile = Hex(0xfff8ec);
        public static readonly Color CreamInk = Hex(0xfff4e4);
        public static readonly Color BoardFace = Hex(0xf7ecd8);
        public static readonly Color BoardEdge = Hex(0xd8c6a8);

        // neutrals
        public static readonly Color Ink900 = Hex(0x2e2b25);
        public static readonly Color Ink800 = Hex(0x4a4338);
        public static readonly Color Ink700 = Hex(0x6b6151);
        public static readonly Color Ink600 = Hex(0x82796a);
        public static readonly Color Line = Hex(0xdcd3c4);
        public static readonly Color LockedFill = Hex(0xe9e2d4);
        public static readonly Color LockedInk = Hex(0xa19786);
        public static readonly Color PanelDark = Hex(0x3a2b22);
        public static readonly Color Umber = Hex(0x402310);

        // gold = progress, amber = affection. Never swapped.
        public static readonly Color Star = Hex(0xffb703);
        public static readonly Color StarLit = Hex(0xffd166);
        public static readonly Color Snack = Hex(0xffd166);

        // board furniture
        public static readonly Color Wall = Hex(0xe0c9a3);
        public static readonly Color WallEdge = Hex(0xc8ab7f);
        public static readonly Color RugA = Hex(0xdfeaf2);
        public static readonly Color RugB = Hex(0xc9dbe8);

        public static Color Warm(float a) => new Color(1f, 0.925f, 0.855f, a);   // #ffecd0-ish
        public static Color Ghost(float a) => new Color(1f, 0.933f, 0.839f, a);  // #ffeed6

        // ---- fonts ----
        // Caprasimo for numbers, names and level titles. Figtree for everything you
        // read. Loaded once; if either is missing we fall back to the old Fredoka
        // rather than dropping to Unity's default sans, which would look nothing
        // like the game.
        private static Font _head, _body, _bodyBold, _fallback;
        private static bool _fontsLoaded;
        private static void LoadFonts()
        {
            if (_fontsLoaded) return;
            _fontsLoaded = true;
            _fallback = Resources.Load<Font>("Fonts/Fredoka");
            _head = Resources.Load<Font>("Fonts/Caprasimo") ?? _fallback;
            _body = Resources.Load<Font>("Fonts/Figtree") ?? _fallback;
            _bodyBold = Resources.Load<Font>("Fonts/FigtreeBold") ?? _body;
        }
        public static Font HeadFont { get { LoadFonts(); return _head; } }
        public static Font BodyFont { get { LoadFonts(); return _body; } }
        public static Font BodyBoldFont { get { LoadFonts(); return _bodyBold; } }

        // Styles are cached per (font, size, colour, alignment, wrap) so a screen can
        // ask for one inside a draw call without allocating a GUIStyle every frame.
        private static readonly Dictionary<long, GUIStyle> _styles = new();
        private static GUIStyle Style(Font f, int px, Color c, TextAnchor a, bool wrap)
        {
            long key = ((long)f.GetInstanceID() << 32) ^ ((long)px << 20) ^ ((long)a << 8)
                       ^ (wrap ? 1 : 0) ^ ((long)(c.r * 255) << 40) ^ ((long)(c.g * 255) << 48)
                       ^ ((long)(c.b * 255) << 56) ^ (long)(c.a * 255);
            if (_styles.TryGetValue(key, out var s)) return s;
            s = new GUIStyle
            {
                font = f,
                fontSize = px,
                alignment = a,
                wordWrap = wrap,
                clipping = TextClipping.Overflow,
                richText = false
            };
            s.normal.textColor = s.hover.textColor = s.active.textColor = c;
            _styles[key] = s;
            return s;
        }

        /// Display type: numbers, names, level titles.
        public static GUIStyle Head(float designPx, Color c, TextAnchor a = TextAnchor.MiddleCenter, bool wrap = false)
            => Style(HeadFont, F(designPx), c, a, wrap);
        /// Reading type.
        public static GUIStyle Body(float designPx, Color c, TextAnchor a = TextAnchor.MiddleCenter, bool wrap = false)
            => Style(BodyFont, F(designPx), c, a, wrap);
        /// Reading type, heavier — the design's 700 weight.
        public static GUIStyle Bold(float designPx, Color c, TextAnchor a = TextAnchor.MiddleCenter, bool wrap = false)
            => Style(BodyBoldFont, F(designPx), c, a, wrap);

        public static void Label(Rect r, string text, GUIStyle st) => GUI.Label(r, text, st);
        public static void Label(float x, float y, float w, float h, string text, GUIStyle st)
            => GUI.Label(R(x, y, w, h), text, st);

        /// Letter-spaced small caps, used for the design's uppercase eyebrow labels
        /// ("CONTINUE", "SOMEONE MOVED IN"). IMGUI has no tracking, so we space the
        /// characters ourselves rather than shipping cramped uppercase.
        public static string Track(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            var sb = new System.Text.StringBuilder(s.Length * 2);
            foreach (var ch in s.ToUpperInvariant()) { sb.Append(ch); sb.Append(' '); }
            return sb.ToString();
        }

        // ---- textures ----
        private static Texture2D _white;
        public static Texture2D White
        {
            get
            {
                if (_white == null)
                {
                    _white = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                    _white.SetPixel(0, 0, Color.white); _white.Apply();
                    _white.hideFlags = HideFlags.HideAndDontSave;
                }
                return _white;
            }
        }

        private static readonly Dictionary<int, Texture2D> _cache = new();
        private static Texture2D Cached(int key, System.Func<Texture2D> make)
        {
            if (_cache.TryGetValue(key, out var t) && t != null) return t;
            t = make(); t.hideFlags = HideFlags.HideAndDontSave;
            _cache[key] = t; return t;
        }

        /// A soft radial disc — glows, halos, lantern light, blanket blobs.
        public static Texture2D Disc => Cached(1, () =>
        {
            const int S1 = 128; var t = NewTex(S1);
            var px = new Color32[S1 * S1]; float c = (S1 - 1) * 0.5f;
            for (int y = 0; y < S1; y++) for (int x = 0; x < S1; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                float a = Mathf.Clamp01(1f - d); a = a * a * (3f - 2f * a);
                px[y * S1 + x] = new Color(1, 1, 1, a);
            }
            t.SetPixels32(px); t.Apply(); return t;
        });

        /// A hard-edged circle with a smooth rim — dots, pips, moons.
        public static Texture2D Dot => Cached(2, () =>
        {
            const int S1 = 128; var t = NewTex(S1);
            var px = new Color32[S1 * S1]; float c = (S1 - 1) * 0.5f;
            for (int y = 0; y < S1; y++) for (int x = 0; x < S1; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                float a = Mathf.Clamp01((c - 0.5f - d) / 1.2f);
                px[y * S1 + x] = new Color(1, 1, 1, a);
            }
            t.SetPixels32(px); t.Apply(); return t;
        });

        /// The five-pointed star from the design's clip-path, as a texture.
        public static Texture2D StarTex => Cached(3, () =>
        {
            const int S1 = 160; var t = NewTex(S1);
            var px = new Color32[S1 * S1];
            // the exact polygon the mockups use, in 0..1 space (y down)
            var poly = new[]
            {
                new Vector2(.50f,.00f), new Vector2(.61f,.35f), new Vector2(.98f,.35f),
                new Vector2(.68f,.57f), new Vector2(.79f,.91f), new Vector2(.50f,.70f),
                new Vector2(.21f,.91f), new Vector2(.32f,.57f), new Vector2(.02f,.35f),
                new Vector2(.39f,.35f)
            };
            for (int y = 0; y < S1; y++) for (int x = 0; x < S1; x++)
            {
                // 2x2 supersample so the points don't come out ragged
                // the polygon is written y-down (as in the mockup's clip-path) and a
                // texture's row 0 is the bottom, so y is flipped — otherwise the
                // star ships pointing downwards
                int hit = 0;
                for (int sy = 0; sy < 2; sy++) for (int sx = 0; sx < 2; sx++)
                {
                    var p = new Vector2((x + 0.25f + sx * 0.5f) / S1,
                                        (S1 - 1 - y + 0.25f + sy * 0.5f) / S1);
                    if (InPoly(poly, p)) hit++;
                }
                px[y * S1 + x] = new Color(1, 1, 1, hit / 4f);
            }
            t.SetPixels32(px); t.Apply(); return t;
        });

        private static bool InPoly(Vector2[] poly, Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if (poly[i].y > p.y != poly[j].y > p.y &&
                    p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    inside = !inside;
            return inside;
        }

        /// The silk-rug hatch: 45-degree stripes, exactly as drawn.
        public static Texture2D RugTex => Cached(4, () =>
        {
            const int S1 = 64; var t = NewTex(S1); t.wrapMode = TextureWrapMode.Repeat;
            var px = new Color32[S1 * S1];
            for (int y = 0; y < S1; y++) for (int x = 0; x < S1; x++)
            {
                int band = Mathf.FloorToInt(((x + y) % 20) / 10f);
                px[y * S1 + x] = band == 0 ? (Color32)RugA : (Color32)RugB;
            }
            t.SetPixels32(px); t.Apply(); return t;
        });

        private static Texture2D NewTex(int s) => new Texture2D(s, s, TextureFormat.RGBA32, false)
        { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };

        /// A vertical gradient strip. Stops are (position 0..1, colour) in order.
        public static Texture2D VGrad(int key, params (float at, Color c)[] stops)
        {
            return Cached(1000 + key, () =>
            {
                const int N = 256;
                var t = new Texture2D(1, N, TextureFormat.RGBA32, false)
                { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                var px = new Color[N];
                for (int i = 0; i < N; i++)
                {
                    // texture v=0 is the BOTTOM, design stop 0 is the TOP
                    float p = 1f - i / (N - 1f);
                    var c = stops[0].c;
                    for (int s = 1; s < stops.Length; s++)
                        if (p <= stops[s].at)
                        {
                            float span = Mathf.Max(1e-5f, stops[s].at - stops[s - 1].at);
                            c = Color.Lerp(stops[s - 1].c, stops[s].c, (p - stops[s - 1].at) / span);
                            break;
                        }
                        else c = stops[s].c;
                    px[i] = c;
                }
                t.SetPixels(px); t.Apply(); return t;
            });
        }

        // ---- primitives ----
        public static void Fill(Rect r, Color c)
        {
            var p = GUI.color; GUI.color = c;
            GUI.DrawTexture(r, White); GUI.color = p;
        }

        /// A rounded rectangle. Unity's IMGUI can round a texture's corners for us,
        /// which is antialiased and costs one draw call — far better than baking a
        /// texture per size.
        public static void Round(Rect r, float radius, Color c)
        {
            float rad = Mathf.Min(P(radius), Mathf.Min(r.width, r.height) * 0.5f);
            GUI.DrawTexture(r, White, ScaleMode.StretchToFill, true, 0f, c,
                            Vector4.zero, new Vector4(rad, rad, rad, rad));
        }

        /// A rounded rectangle filled with a vertical gradient.
        public static void RoundGrad(Rect r, float radius, Texture2D grad)
        {
            float rad = Mathf.Min(P(radius), Mathf.Min(r.width, r.height) * 0.5f);
            GUI.DrawTexture(r, grad, ScaleMode.StretchToFill, true, 0f, Color.white,
                            Vector4.zero, new Vector4(rad, rad, rad, rad));
        }

        /// A rounded outline: drawn as a filled rounded rect with a second one
        /// punched out of it, so the stroke stays crisp at any radius.
        public static void RoundOutline(Rect r, float radius, float thickness, Color stroke, Color fill)
        {
            Round(r, radius, stroke);
            float t = P(thickness);
            var inner = new Rect(r.x + t, r.y + t, r.width - t * 2, r.height - t * 2);
            if (inner.width > 0 && inner.height > 0)
            {
                float ir = Mathf.Max(0f, radius - thickness);
                float rad = Mathf.Min(P(ir), Mathf.Min(inner.width, inner.height) * 0.5f);
                GUI.DrawTexture(inner, White, ScaleMode.StretchToFill, true, 0f, fill,
                                Vector4.zero, new Vector4(rad, rad, rad, rad));
            }
        }

        public static void Circle(Rect r, Color c)
        {
            var p = GUI.color; GUI.color = c;
            GUI.DrawTexture(r, Dot); GUI.color = p;
        }

        /// A soft glow centred on a rect, spreading `spread` design units past it.
        public static void Glow(Rect r, float spread, Color c)
        {
            float s = P(spread);
            var p = GUI.color; GUI.color = c;
            GUI.DrawTexture(new Rect(r.x - s, r.y - s, r.width + s * 2, r.height + s * 2), Disc);
            GUI.color = p;
        }

        public static void StarShape(Rect r, Color c)
        {
            var p = GUI.color; GUI.color = c;
            GUI.DrawTexture(r, StarTex); GUI.color = p;
        }

        /// A row of three stars, as used on level nodes and the win screen.
        public static void Stars(float cx, float y, float size, float gap, int lit, Color on, Color off)
        {
            float total = size * 3 + gap * 2;
            for (int i = 0; i < 3; i++)
                StarShape(R(cx - total * 0.5f + i * (size + gap), y, size, size), i < lit ? on : off);
        }

        // ---- widgets ----
        /// True while this rect is being held down. Used for the primary button's
        /// press, which sinks onto its own bottom edge instead of changing colour.
        private static bool Held(Rect r) =>
            r.Contains(Event.current.mousePosition) && Input.GetMouseButton(0);

        /// THE primary. One per screen: terracotta, chunky, with a pressable bottom
        /// edge and a drop shadow. Nothing else in the game gets this treatment.
        public static bool Primary(float x, float y, float w, float h, string label, float fontPx = 24f,
                                   float radius = 999f)
        {
            var outer = R(x, y, w, h);
            bool down = Held(outer);
            float lift = down ? P(2f) : P(4f);
            float edge = P(7f);

            // shadow, then the darker base that the face sits proud of
            Round(new Rect(outer.x, outer.y + P(9f), outer.width, outer.height), radius,
                  new Color(PrimaryShadow.r, PrimaryShadow.g, PrimaryShadow.b, 0.40f));
            Round(new Rect(outer.x, outer.y, outer.width, outer.height + edge), radius, PrimaryBase);

            var face = new Rect(outer.x, outer.y - lift + edge, outer.width, outer.height);
            RoundGrad(face, radius, VGrad(1, (0f, PrimaryTop), (1f, PrimaryBot)));
            GUI.Label(face, label, Head(fontPx, PrimaryInk));

            return GUI.Button(new Rect(outer.x, outer.y, outer.width, outer.height + edge),
                              GUIContent.none, GUIStyle.none);
        }

        /// Secondary: an outline pill. Never filled, never terracotta.
        public static bool Outline(float x, float y, float w, float h, string label, float fontPx = 14f,
                                   float radius = 999f, bool headFont = false)
        {
            var r = R(x, y, w, h);
            RoundOutline(r, radius, 2f, Ghost(Held(r) ? 0.55f : 0.32f), new Color(0, 0, 0, 0f));
            GUI.Label(r, label, headFont ? Head(fontPx, Ghost(0.94f)) : Bold(fontPx, Ghost(0.94f)));
            return GUI.Button(r, GUIContent.none, GUIStyle.none);
        }

        /// Tertiary: a round icon on a ghost disc.
        public static bool GhostDisc(float x, float y, float d, Texture2D icon, float iconScale = 0.45f,
                                     Color? tint = null, bool ring = true)
        {
            var r = R(x, y, d, d);
            Round(r, d * 0.5f, Ghost(Held(r) ? 0.24f : 0.14f));
            if (ring) RoundOutline(r, d * 0.5f, 1f, Ghost(0.22f), new Color(0, 0, 0, 0f));
            if (icon != null)
            {
                float s = d * iconScale;
                var p = GUI.color; GUI.color = tint ?? Ghost(0.94f);
                GUI.DrawTexture(R(x + (d - s) * 0.5f, y + (d - s) * 0.5f, s, s), icon);
                GUI.color = p;
            }
            return GUI.Button(r, GUIContent.none, GUIStyle.none);
        }

        /// A small translucent capsule holding an icon and a number — the star count,
        /// the snack count, the moves left.
        public static Rect Chip(float x, float y, float h, string text, Texture2D icon, Color iconTint,
                                Color ink, Color bg, float fontPx = 15f, string suffix = null,
                                Color? suffixInk = null)
        {
            float pad = h * 0.42f, ic = h * 0.44f, gap = h * 0.18f;
            var st = Head(fontPx, ink);
            float tw = st.CalcSize(new GUIContent(text)).x / S;
            float sw = 0f;
            GUIStyle ss = null;
            if (!string.IsNullOrEmpty(suffix))
            {
                ss = Bold(fontPx * 0.75f, suffixInk ?? new Color(ink.r, ink.g, ink.b, 0.5f));
                sw = ss.CalcSize(new GUIContent(suffix)).x / S + gap;
            }
            float w = pad + (icon != null ? ic + gap : 0f) + tw + sw + pad;
            var r = R(x, y, w, h);
            Round(r, h * 0.5f, bg);
            float cx = x + pad;
            if (icon != null)
            {
                var p = GUI.color; GUI.color = iconTint;
                GUI.DrawTexture(R(cx, y + (h - ic) * 0.5f, ic, ic), icon);
                GUI.color = p;
                cx += ic + gap;
            }
            GUI.Label(R(cx, y, tw, h), text, Head(fontPx, ink, TextAnchor.MiddleLeft));
            if (ss != null) GUI.Label(R(cx + tw + gap, y, sw, h), suffix, Bold(fontPx * 0.75f,
                suffixInk ?? new Color(ink.r, ink.g, ink.b, 0.5f), TextAnchor.MiddleLeft));
            return new Rect(x, y, w, h);
        }

        /// The progress bar used for "stars until the next chapter / next friend".
        public static void Bar(float x, float y, float w, float h, float t, Color track,
                               Color from, Color to)
        {
            Round(R(x, y, w, h), h * 0.5f, track);
            float fw = Mathf.Clamp01(t) * w;
            if (fw <= 0.01f) return;
            // two-stop horizontal ramp, faked with a few slices (IMGUI has no
            // horizontal gradient primitive and one is not worth a shader)
            const int N = 24;
            for (int i = 0; i < N; i++)
            {
                float a = i / (float)N, b = (i + 1) / (float)N;
                var c = Color.Lerp(from, to, (a + b) * 0.5f);
                var seg = R(x + a * fw, y, (b - a) * fw + P(0.5f) / S, h);
                if (i == 0) Round(new Rect(seg.x, seg.y, seg.width + P(h), seg.height), h * 0.5f, c);
                else Fill(seg, c);
            }
            // round off the leading end
            if (fw > h) Round(R(x + fw - h, y, h, h), h * 0.5f, Color.Lerp(from, to, 1f));
        }

        /// A full-bleed vertical gradient behind a screen.
        public static void Backdrop(int key, params (float at, Color c)[] stops)
        {
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), VGrad(key, stops),
                            ScaleMode.StretchToFill);
        }

        /// The soft hill silhouette that grounds the home and board screens.
        public static void Hill(float y, float h, Color c)
        {
            // an ellipse cap: drawn as a very round rounded-rect, which is what the
            // design's `border-radius: 50% 50% 0 0` amounts to at this size
            var r = R(-40f, y, DesignW + 80f, h * 2f);
            GUI.DrawTexture(r, White, ScaleMode.StretchToFill, true, 0f, c, Vector4.zero,
                            new Vector4(P(h), P(h), 0, 0));
        }

        // ---- misc ----
        /// Wraps `text` to `w` design units and returns the height it needs.
        public static float TextHeight(string text, GUIStyle st, float w)
            => st.CalcHeight(new GUIContent(text), P(w)) / S;
    }
}
