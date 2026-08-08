using System.Collections.Generic;
using UnityEngine;

namespace TuckIn
{
    /// <summary>
    /// The redesign's icon set, drawn in code.
    ///
    /// The mockups use 24x24 SVG icons with a 2.75 round-capped stroke. Unity has no
    /// vector support in IMGUI, and shipping a PNG per icon per density would mean
    /// twenty more files to keep in sync with a design that is still moving. So the
    /// icons are rasterised once at startup from the same 24-unit coordinates the
    /// SVGs use: a signed-distance pass over polylines for the stroked ones and a
    /// scanline fill for the solid ones.
    ///
    /// Every icon is white. Tint at draw time — that is what keeps a gold star, an
    /// amber snack and a cream chevron from needing three textures.
    /// </summary>
    public static class Icons
    {
        private const int Res = 128;          // texture size
        private const float View = 24f;       // SVG viewBox
        private const float Stroke = 2.75f;   // design stroke width

        private static readonly Dictionary<string, Texture2D> _cache = new();

        public static Texture2D Get(string name)
        {
            if (_cache.TryGetValue(name, out var t) && t != null) return t;
            t = Build(name);
            t.hideFlags = HideFlags.HideAndDontSave;
            _cache[name] = t;
            return t;
        }

        // convenience handles, so call sites read like the design
        public static Texture2D Speaker => Get("speaker");
        public static Texture2D SpeakerOff => Get("speaker_off");
        public static Texture2D Gear => Get("gear");
        public static Texture2D Map => Get("map");
        public static Texture2D Bed => Get("bed");
        public static Texture2D Moon => Get("moon");
        public static Texture2D Play => Get("play");
        public static Texture2D Chevron => Get("chevron");
        public static Texture2D Snack => Get("snack");
        public static Texture2D Heart => Get("heart");
        public static Texture2D Bulb => Get("bulb");
        public static Texture2D Undo => Get("undo");
        public static Texture2D Reset => Get("reset");
        public static Texture2D Lock => Get("lock");
        public static Texture2D Share => Get("share");
        public static Texture2D Pencil => Get("pencil");
        public static Texture2D Close => Get("close");
        public static Texture2D Buzz => Get("buzz");
        public static Texture2D Ball => Get("ball");
        public static Texture2D Pillow => Get("pillow");
        public static Texture2D Note => Get("note");
        public static Texture2D Broom => Get("broom");

        // ---- geometry helpers (all in 24-unit SVG space) ----
        private static Vector2 V(float x, float y) => new Vector2(x, y);

        /// Sample an arc into a polyline. Angles in degrees, 0 = +x, clockwise in
        /// screen space (y down), which is how the SVG paths read.
        private static Vector2[] Arc(float cx, float cy, float r, float a0, float a1, int seg = 20)
        {
            var p = new Vector2[seg + 1];
            for (int i = 0; i <= seg; i++)
            {
                float a = Mathf.Lerp(a0, a1, i / (float)seg) * Mathf.Deg2Rad;
                p[i] = V(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r);
            }
            return p;
        }

        private static Vector2[] Circle(float cx, float cy, float r, int seg = 40)
            => Arc(cx, cy, r, 0f, 360f, seg);

        private static Vector2[] Join(params Vector2[][] parts)
        {
            var list = new List<Vector2>();
            foreach (var p in parts)
                foreach (var v in p)
                    if (list.Count == 0 || (list[list.Count - 1] - v).sqrMagnitude > 1e-6f) list.Add(v);
            return list.ToArray();
        }

        // ---- the icons ----
        private static Texture2D Build(string name)
        {
            var strokes = new List<Vector2[]>();
            var fills = new List<Vector2[]>();
            float w = Stroke;

            switch (name)
            {
                case "speaker":
                case "speaker_off":
                    // M11 5 6 9H2v6h4l5 4V5Z — the cone, filled
                    fills.Add(new[] { V(11, 5), V(6, 9), V(2, 9), V(2, 15), V(6, 15), V(11, 19) });
                    if (name == "speaker")
                    {
                        strokes.Add(Arc(11f, 12f, 5.2f, -46f, 46f));    // near wave
                        strokes.Add(Arc(11f, 12f, 8.4f, -42f, 42f));    // far wave
                    }
                    else
                    {
                        strokes.Add(new[] { V(16.5f, 9.5f), V(21.5f, 14.5f) });
                        strokes.Add(new[] { V(21.5f, 9.5f), V(16.5f, 14.5f) });
                    }
                    break;

                case "gear":
                    {
                        // a cog: eight teeth around a ring, then the hole punched out
                        const int teeth = 8;
                        var pts = new List<Vector2>();
                        for (int i = 0; i < teeth * 4; i++)
                        {
                            float a = i / (float)(teeth * 4) * 360f;
                            int phase = i % 4;
                            float r = phase == 1 || phase == 2 ? 10.2f : 7.4f;
                            pts.Add(V(12 + Mathf.Cos(a * Mathf.Deg2Rad) * r,
                                      12 + Mathf.Sin(a * Mathf.Deg2Rad) * r));
                        }
                        fills.Add(pts.ToArray());
                        // the hole: a reverse-wound circle, handled by the even-odd fill
                        fills.Add(Circle(12, 12, 3.6f));
                        break;
                    }

                case "map":
                    // m3 6 6-3 6 3 6-3v15l-6 3-6-3-6 3V6Z plus the two folds
                    strokes.Add(new[] { V(3, 6), V(9, 3), V(15, 6), V(21, 3), V(21, 18),
                                        V(15, 21), V(9, 18), V(3, 21), V(3, 6) });
                    strokes.Add(new[] { V(9, 3), V(9, 18) });
                    strokes.Add(new[] { V(15, 6), V(15, 21) });
                    break;

                case "bed":
                    // M2 18v-6a4 4 0 0 1 4-4h12a4 4 0 0 1 4 4v6  + mattress line + headboard
                    strokes.Add(Join(new[] { V(2, 18), V(2, 12) }, Arc(6, 12, 4, 180, 270),
                                     new[] { V(18, 8) }, Arc(18, 12, 4, 270, 360),
                                     new[] { V(22, 18) }));
                    strokes.Add(new[] { V(2, 15), V(22, 15) });
                    strokes.Add(Join(new[] { V(6, 8), V(6, 6) }, Arc(8, 6, 2, 180, 270),
                                     new[] { V(16, 4) }, Arc(16, 6, 2, 270, 360),
                                     new[] { V(18, 8) }));
                    break;

                case "moon":
                    {
                        // M12 3a6 6 0 0 0 9 9 9 9 0 1 1-9-9Z — a crescent. The punch
                        // circle has to sit well INSIDE the disc, otherwise it takes a
                        // bite out of the edge instead of carving a crescent.
                        fills.Add(Circle(12, 12, 9.2f, 48));
                        fills.Add(Circle(17.4f, 6.6f, 8.8f, 48));   // punched out
                        break;
                    }

                case "play":
                    fills.Add(new[] { V(8, 4.6f), V(20.4f, 12), V(8, 19.4f) });
                    break;

                case "chevron":
                    strokes.Add(new[] { V(15, 18), V(9, 12), V(15, 6) });
                    break;

                case "close":
                    strokes.Add(new[] { V(6.5f, 6.5f), V(17.5f, 17.5f) });
                    strokes.Add(new[] { V(17.5f, 6.5f), V(6.5f, 17.5f) });
                    break;

                case "snack":
                    {
                        // M12 2.7 6.5 9.5a7 7 0 1 0 11 0Z — a drop. Apex at the top,
                        // then the body circle swept the long way round (angles are
                        // y-down here, so 90 is the BOTTOM of the circle).
                        var pts = new List<Vector2> { V(12, 2.4f) };
                        pts.AddRange(Arc(12, 13.4f, 7.0f, -48f, 228f, 44));
                        fills.Add(pts.ToArray());
                        break;
                    }

                case "heart":
                    {
                        // The classic parametric heart. Two arcs plus a point kept
                        // coming out as a diamond; this is one closed curve and it
                        // cannot go wrong.
                        var pts = new List<Vector2>();
                        for (int i = 0; i < 60; i++)
                        {
                            float t = i / 60f * Mathf.PI * 2f;
                            float s = Mathf.Sin(t);
                            float px2 = 12f + 0.55f * 16f * s * s * s;
                            float py2 = 12f - 0.55f * (13f * Mathf.Cos(t) - 5f * Mathf.Cos(2 * t)
                                                       - 2f * Mathf.Cos(3 * t) - Mathf.Cos(4 * t));
                            pts.Add(V(px2, py2));
                        }
                        fills.Add(pts.ToArray());
                        break;
                    }

                case "bulb":
                    strokes.Add(Join(Arc(12, 9.4f, 6f, 120f, 420f, 30)));
                    strokes.Add(new[] { V(9, 15.6f), V(9, 17.2f) });
                    strokes.Add(new[] { V(15, 15.6f), V(15, 17.2f) });
                    strokes.Add(new[] { V(9, 17.2f), V(15, 17.2f) });
                    strokes.Add(new[] { V(10, 20), V(14, 20) });
                    break;

                case "undo":
                    // M9 14 4 9l5-5  +  M4 9h11a5 5 0 0 1 0 10h-4
                    strokes.Add(new[] { V(9, 14), V(4, 9), V(9, 4) });
                    strokes.Add(Join(new[] { V(4, 9), V(15, 9) }, Arc(15, 14, 5, -90, 90), new[] { V(11, 19) }));
                    break;

                case "reset":
                    strokes.Add(Join(Arc(12, 12, 9, 200f, 520f, 34)));
                    strokes.Add(new[] { V(3, 3), V(3, 8), V(8, 8) });
                    break;

                case "lock":
                    strokes.Add(new[] { V(4, 10), V(20, 10), V(20, 21), V(4, 21), V(4, 10) });
                    strokes.Add(Join(new[] { V(8, 10), V(8, 7) }, Arc(12, 7, 4, 180, 360), new[] { V(16, 10) }));
                    break;

                case "share":
                    fills.Add(Circle(18, 5.5f, 3.1f));
                    fills.Add(Circle(6, 12, 3.1f));
                    fills.Add(Circle(18, 18.5f, 3.1f));
                    strokes.Add(new[] { V(8.6f, 10.7f), V(15.4f, 6.8f) });
                    strokes.Add(new[] { V(8.6f, 13.3f), V(15.4f, 17.2f) });
                    w = 2.0f;
                    break;

                case "pencil":
                    strokes.Add(new[] { V(17.6f, 3.2f), V(20.8f, 6.4f), V(7.5f, 20.5f),
                                        V(2f, 22f), V(3.5f, 16.5f), V(17.6f, 3.2f) });
                    w = 2.2f;
                    break;

                case "buzz":
                    // vibration: a phone with waves either side
                    strokes.Add(new[] { V(8, 3), V(16, 3), V(16, 21), V(8, 21), V(8, 3) });
                    strokes.Add(Arc(12, 12, 8.6f, 140f, 220f, 14));
                    strokes.Add(Arc(12, 12, 8.6f, -40f, 40f, 14));
                    break;

                case "ball":
                    // a play ball: a circle with a seam, so it never reads as a dot
                    strokes.Add(Circle(12, 12, 8.6f));
                    strokes.Add(Arc(4.6f, 12f, 8.4f, -62f, 62f, 18));
                    strokes.Add(Arc(19.4f, 12f, 8.4f, 118f, 242f, 18));
                    break;

                case "pillow":
                    {
                        // the Pillow power-up: a plump cushion with dented corners.
                        // Drawn as a closed curve rather than a rectangle, because a
                        // rectangle with a mark in it just reads as a card.
                        var pts = new List<Vector2>();
                        for (int i = 0; i < 48; i++)
                        {
                            float a = i / 48f * Mathf.PI * 2f;
                            // a superellipse, pinched in at the sides
                            float cs = Mathf.Cos(a), sn = Mathf.Sin(a);
                            float rx = 8.6f - 1.6f * Mathf.Abs(sn);
                            float ry = 6.2f - 1.2f * Mathf.Abs(cs);
                            pts.Add(V(12f + cs * rx, 12f + sn * ry));
                        }
                        strokes.Add(pts.ToArray());
                        // the crease
                        strokes.Add(Arc(12f, 16.6f, 4.4f, -140f, -40f, 12));
                        w = 2.3f;
                        break;
                    }

                case "note":
                    // the Lullaby power-up: a single music note
                    fills.Add(Circle(8.4f, 17.6f, 3.4f));
                    strokes.Add(new[] { V(11.6f, 17.6f), V(11.6f, 4.6f), V(19.4f, 6.8f) });
                    strokes.Add(new[] { V(11.6f, 9.6f), V(19.4f, 11.8f) });
                    w = 2.2f;
                    break;

                case "broom":
                    // the Tidy up power-up: sweep a block away
                    strokes.Add(new[] { V(17.6f, 4.4f), V(10.4f, 11.6f) });
                    strokes.Add(new[] { V(12.6f, 9.4f), V(16.4f, 13.2f) });
                    fills.Add(new[] { V(11.2f, 12.4f), V(15.6f, 16.8f), V(9.4f, 21.2f),
                                      V(4.2f, 19.4f), V(6.0f, 14.2f) });
                    w = 2.4f;
                    break;

                default:
                    strokes.Add(Circle(12, 12, 8f));
                    break;
            }

            return Raster(strokes, fills, w);
        }

        // ---- rasteriser ----
        private static Texture2D Raster(List<Vector2[]> strokes, List<Vector2[]> fills, float strokeW)
        {
            var tex = new Texture2D(Res, Res, TextureFormat.RGBA32, false)
            { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[Res * Res];

            float k = Res / View;                 // SVG units -> pixels
            float rad = strokeW * 0.5f * k;       // stroke half-width in pixels

            for (int y = 0; y < Res; y++)
                for (int x = 0; x < Res; x++)
                {
                    // Sample at the pixel centre, in SVG space. SVG's y grows
                    // downward and a Unity texture's row 0 is the BOTTOM, so the y
                    // axis is flipped here — without this every asymmetric icon
                    // (the bed, the lock, the arrows) ships upside down.
                    var p = new Vector2((x + 0.5f) / k, (Res - 1 - y + 0.5f) / k);

                    float a = 0f;

                    // stroked paths: coverage from the distance to the nearest segment,
                    // which gives round caps and joins for free
                    if (strokes.Count > 0)
                    {
                        float best = float.MaxValue;
                        foreach (var path in strokes)
                            for (int i = 0; i < path.Length - 1; i++)
                                best = Mathf.Min(best, SegDist(p, path[i], path[i + 1]));
                        a = Mathf.Max(a, Mathf.Clamp01((rad / k - best) * k + 0.5f));
                    }

                    // filled shapes: even-odd, so a second ring punches a hole in the
                    // first (that is how the gear and the crescent moon are built)
                    if (fills.Count > 0)
                    {
                        int crossings = 0;
                        foreach (var poly in fills) if (InPoly(poly, p)) crossings++;
                        if ((crossings & 1) == 1) a = 1f;
                        else if (crossings > 0) a = Mathf.Max(a, 0f);
                    }

                    px[y * Res + x] = new Color(1f, 1f, 1f, a);
                }

            // one box blur pass, which is enough to take the stair-stepping off the
            // filled shapes without softening the strokes into mush
            var outPx = new Color32[Res * Res];
            for (int y = 0; y < Res; y++)
                for (int x = 0; x < Res; x++)
                {
                    int sum = 0, n = 0;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int sx = x + dx, sy = y + dy;
                            if (sx < 0 || sy < 0 || sx >= Res || sy >= Res) continue;
                            sum += px[sy * Res + sx].a; n++;
                        }
                    outPx[y * Res + x] = new Color32(255, 255, 255, (byte)(sum / Mathf.Max(1, n)));
                }

            tex.SetPixels32(outPx);
            tex.Apply();
            return tex;
        }

        private static float SegDist(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float len2 = ab.sqrMagnitude;
            if (len2 < 1e-8f) return (p - a).magnitude;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
            return (p - (a + ab * t)).magnitude;
        }

        private static bool InPoly(Vector2[] poly, Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if (poly[i].y > p.y != poly[j].y > p.y &&
                    p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    inside = !inside;
            return inside;
        }
    }
}
