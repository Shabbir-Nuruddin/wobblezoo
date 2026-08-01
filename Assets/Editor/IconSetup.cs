using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace ChonkyMerge.EditorTools
{
    /// <summary>
    /// Builds the Android app icon and assigns every slot Unity offers.
    ///
    /// This exists because the project shipped with all eighteen Android icon slots
    /// EMPTY — which doesn't fail a build, it just quietly hands Google Play the
    /// default Unity logo. The only way to notice is to install the app and look at
    /// your home screen.
    ///
    /// Android's adaptive icons are the fiddly part: the launcher masks the icon to
    /// whatever shape the phone likes (circle, squircle, teardrop), and it can crop
    /// up to a third of the canvas. So the foreground is drawn INSIDE a safe zone
    /// rather than filling the frame — an icon that looks right as a square loses its
    /// ears the moment a round mask goes over it.
    ///
    /// Everything is generated from art already in the repo, so re-running this after
    /// an art change keeps the icon honest.
    ///
    ///     Unity.exe -batchmode -quit -projectPath . \
    ///               -executeMethod ChonkyMerge.EditorTools.IconSetup.Run
    /// </summary>
    public static class IconSetup
    {
        private const string CatPath = "Assets/Resources/Art/critter_cat.png";
        private const string SourceIcon = "Assets/Resources/Art/AppIcon.png";
        private const string OutDir = "Assets/Art/Icons";

        // Android's adaptive canvas is 108dp and only the middle 72dp is guaranteed
        // to survive the mask. Everything that matters lives inside this fraction.
        private const float SafeZone = 72f / 108f;

        [MenuItem("Chonky/Set Up App Icons")]
        public static void Run()
        {
            Directory.CreateDirectory(OutDir);

            var cat = Load(CatPath);
            if (cat == null) { Debug.LogError("IconSetup: missing " + CatPath); return; }

            // Take the gradient from the existing icon so the app icon, the store icon
            // and the menu all stay the same warm palette.
            Color top = new Color(0.98f, 0.85f, 0.66f), bottom = new Color(0.97f, 0.71f, 0.67f);
            var src = Load(SourceIcon);
            if (src != null)
            {
                top = src.GetPixel(src.width / 2, src.height - 2);
                bottom = src.GetPixel(src.width / 2, 2);
            }

            // --- adaptive background: gradient only, full bleed (it WILL be cropped) ---
            var bg = new Texture2D(432, 432, TextureFormat.RGBA32, false);
            Gradient(bg, top, bottom);
            Save(bg, OutDir + "/icon_adaptive_background.png");

            // --- adaptive foreground: transparent, cat on a white disc, inside the safe zone ---
            var fg = new Texture2D(432, 432, TextureFormat.RGBA32, false);
            Clear(fg);
            DrawBadge(fg, cat, SafeZone);
            Save(fg, OutDir + "/icon_adaptive_foreground.png");

            // --- legacy (square) and round: the whole thing, composited ---
            var legacy = new Texture2D(512, 512, TextureFormat.RGBA32, false);
            Gradient(legacy, top, bottom);
            DrawBadge(legacy, cat, 0.92f);
            Save(legacy, OutDir + "/icon_legacy.png");

            var round = new Texture2D(512, 512, TextureFormat.RGBA32, false);
            Clear(round);
            GradientDisc(round, top, bottom);
            DrawBadge(round, cat, 0.86f);
            Save(round, OutDir + "/icon_round.png");

            // --- a 512 store icon for the Play listing (not used by the build) ---
            var store = new Texture2D(512, 512, TextureFormat.RGBA32, false);
            Gradient(store, top, bottom);
            DrawBadge(store, cat, 0.92f);
            Directory.CreateDirectory("store");
            File.WriteAllBytes("store/play_icon_512.png", store.EncodeToPNG());

            FeatureGraphic(top, bottom);

            AssetDatabase.Refresh();
            foreach (var f in new[] { "icon_adaptive_background", "icon_adaptive_foreground",
                                      "icon_legacy", "icon_round" })
                MakeIconAsset(OutDir + "/" + f + ".png");
            AssetDatabase.Refresh();

            Assign();
            Debug.Log("IconSetup: icons generated and every Android slot assigned.");
        }

        // ---- assignment ----
        private static void Assign()
        {
            var bg = AssetDatabase.LoadAssetAtPath<Texture2D>(OutDir + "/icon_adaptive_background.png");
            var fg = AssetDatabase.LoadAssetAtPath<Texture2D>(OutDir + "/icon_adaptive_foreground.png");
            var legacy = AssetDatabase.LoadAssetAtPath<Texture2D>(OutDir + "/icon_legacy.png");
            var round = AssetDatabase.LoadAssetAtPath<Texture2D>(OutDir + "/icon_round.png");
            if (bg == null || fg == null || legacy == null || round == null)
            { Debug.LogError("IconSetup: generated icons did not import."); return; }

            var target = NamedBuildTarget.Android;
            // Enumerated rather than named: AndroidPlatformIconKind lives in the Android
            // module's own assembly, which an Editor script doesn't reference by default.
            foreach (var kind in PlayerSettings.GetSupportedIconKinds(target))
            {
                var icons = PlayerSettings.GetPlatformIcons(target, kind);
                string name = kind.ToString().ToLowerInvariant();
                for (int i = 0; i < icons.Length; i++)
                {
                    // Two layers means adaptive, and Unity's layer order is
                    // (background, foreground) — getting it backwards buries the cat.
                    if (icons[i].maxLayerCount >= 2) icons[i].SetTextures(bg, fg);
                    else if (name.Contains("round")) icons[i].SetTextures(round);
                    else icons[i].SetTextures(legacy);
                }
                PlayerSettings.SetPlatformIcons(target, kind, icons);
                Debug.Log($"IconSetup: assigned {icons.Length} '{kind}' icon(s).");
            }
            // The old single-icon API drives the store/Standalone icon as well.
            PlayerSettings.SetIcons(target, new[] { legacy }, IconKind.Application);
            AssetDatabase.SaveAssets();
        }

        /// Play's 1024x500 banner. It gets cropped hard on some surfaces and is often
        /// shown at thumbnail size, so: logo big and centred, animals kept clear of the
        /// edges, and nothing important in the outer margin.
        private static void FeatureGraphic(Color top, Color bottom)
        {
            const int W = 1024, H = 500;
            var g = new Texture2D(W, H, TextureFormat.RGBA32, false);
            Gradient(g, top, bottom);

            // Clouds, built as clusters of overlapping discs rather than single circles —
            // one disc reads as a blob. Kept faint and out of the middle band so they sit
            // behind the artwork instead of competing with it.
            var rng = new System.Random(7);
            for (int i = 0; i < 7; i++)
            {
                float x = (float)rng.NextDouble() * W;
                float y = (i % 2 == 0) ? H * (0.80f + (float)rng.NextDouble() * 0.18f)
                                       : H * (0.04f + (float)rng.NextDouble() * 0.16f);
                float r = 34f + (float)rng.NextDouble() * 26f;
                var white = new Color(1f, 1f, 1f, 0.20f);
                SoftDisc(g, x, y, r, white);
                SoftDisc(g, x - r * 0.85f, y - r * 0.20f, r * 0.72f, white);
                SoftDisc(g, x + r * 0.85f, y - r * 0.20f, r * 0.66f, white);
            }

            var logo = Load("Assets/Resources/Art/Logo.png");
            if (logo != null)
            {
                float lw = W * 0.46f, lh = lw * logo.height / (float)logo.width;
                Blit(g, logo, W * 0.05f, (H - lh) * 0.5f, lw, lh);
            }

            // Three friends on the right, spaced so nobody sits on top of anybody, and
            // inset from the edge so Play's crop can't behead one.
            string[] who = { "dog", "rabbit", "panda" };
            float step = W * 0.145f, box = W * 0.135f;
            float bx = W * 0.605f, by = H * 0.5f;
            for (int i = 0; i < who.Length; i++)
            {
                var art = Load("Assets/Resources/Art/pets/" + who[i] + ".png");
                if (art == null) continue;
                // Fit inside the box preserving aspect — scaling to a square is what
                // stretched everyone into a thin oval.
                float aspect = art.width / (float)art.height;
                float aw = aspect >= 1f ? box : box * aspect;
                float ah = aspect >= 1f ? box / aspect : box;
                float cx = bx + i * step, cy = by + (i % 2 == 0 ? 12f : -16f);
                SoftDisc(g, cx, cy, box * 0.62f, Color.white);
                Blit(g, art, cx - aw * 0.5f, cy - ah * 0.5f, aw, ah);
            }

            File.WriteAllBytes("store/feature_graphic.png", g.EncodeToPNG());
        }

        // ---- drawing ----
        private static void Clear(Texture2D t)
        {
            var px = new Color32[t.width * t.height];
            t.SetPixels32(px); t.Apply();
        }

        private static void Gradient(Texture2D t, Color top, Color bottom)
        {
            var px = new Color[t.width * t.height];
            for (int y = 0; y < t.height; y++)
            {
                var c = Color.Lerp(bottom, top, y / (float)(t.height - 1));
                for (int x = 0; x < t.width; x++) px[y * t.width + x] = c;
            }
            t.SetPixels(px); t.Apply();
        }

        /// The same gradient, clipped to a circle — for launchers that ask for a round icon.
        private static void GradientDisc(Texture2D t, Color top, Color bottom)
        {
            float cx = t.width * 0.5f, cy = t.height * 0.5f, r = t.width * 0.5f - 1f;
            var px = new Color[t.width * t.height];
            for (int y = 0; y < t.height; y++)
                for (int x = 0; x < t.width; x++)
                {
                    var c = Color.Lerp(bottom, top, y / (float)(t.height - 1));
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    c.a = Mathf.Clamp01(r - d);          // one-pixel feather, no jaggies
                    px[y * t.width + x] = c;
                }
            t.SetPixels(px); t.Apply();
        }

        /// White disc with the animal resting on it, sized to `fraction` of the canvas.
        private static void DrawBadge(Texture2D dst, Texture2D art, float fraction)
        {
            float size = dst.width * fraction;
            float cx = dst.width * 0.5f, cy = dst.height * 0.5f;
            SoftDisc(dst, cx, cy, size * 0.5f, Color.white);
            float aw = size * 0.86f;      // fill the disc; a thick white ring reads as padding
            Blit(dst, art, cx - aw * 0.5f, cy - aw * 0.5f, aw, aw);
        }

        private static void SoftDisc(Texture2D t, float cx, float cy, float r, Color col)
        {
            int x0 = Mathf.Max(0, (int)(cx - r) - 2), x1 = Mathf.Min(t.width - 1, (int)(cx + r) + 2);
            int y0 = Mathf.Max(0, (int)(cy - r) - 2), y1 = Mathf.Min(t.height - 1, (int)(cy + r) + 2);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    float a = Mathf.Clamp01(r - d);
                    if (a <= 0f) continue;
                    t.SetPixel(x, y, Over(new Color(col.r, col.g, col.b, a), t.GetPixel(x, y)));
                }
            t.Apply();
        }

        private static void Blit(Texture2D dst, Texture2D src, float dx, float dy, float dw, float dh)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt(dx)), x1 = Mathf.Min(dst.width - 1, Mathf.CeilToInt(dx + dw));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(dy)), y1 = Mathf.Min(dst.height - 1, Mathf.CeilToInt(dy + dh));
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float u = (x - dx) / dw, v = (y - dy) / dh;
                    if (u < 0f || u > 1f || v < 0f || v > 1f) continue;
                    // No V flip: source and destination are both Texture2D, so both are
                    // already bottom-up. Flipping here is what stood the cat on its head.
                    var s = src.GetPixelBilinear(u, v);
                    if (s.a <= 0.001f) continue;
                    dst.SetPixel(x, y, Over(s, dst.GetPixel(x, y)));
                }
            dst.Apply();
        }

        private static Color Over(Color fg, Color bg)
        {
            float a = fg.a + bg.a * (1f - fg.a);
            if (a <= 0f) return new Color(0, 0, 0, 0);
            var rgb = (new Vector3(fg.r, fg.g, fg.b) * fg.a
                     + new Vector3(bg.r, bg.g, bg.b) * bg.a * (1f - fg.a)) / a;
            return new Color(rgb.x, rgb.y, rgb.z, a);
        }

        // ---- io ----
        /// Loads a PNG through its bytes rather than the asset, so it doesn't matter
        /// whether the importer marked it readable.
        private static Texture2D Load(string path)
        {
            if (!File.Exists(path)) return null;
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            return t.LoadImage(File.ReadAllBytes(path)) ? t : null;
        }

        private static void Save(Texture2D t, string path) =>
            File.WriteAllBytes(path, t.EncodeToPNG());

        /// Icons must import uncompressed and unmodified — a compressed or resized
        /// icon shows up as mush on a home screen.
        private static void MakeIconAsset(string path)
        {
            var im = AssetImporter.GetAtPath(path) as TextureImporter;
            if (im == null) return;
            im.textureType = TextureImporterType.Default;
            im.npotScale = TextureImporterNPOTScale.None;
            im.mipmapEnabled = false;
            im.alphaIsTransparency = true;
            im.maxTextureSize = 1024;
            im.textureCompression = TextureImporterCompression.Uncompressed;
            im.SaveAndReimport();
        }
    }
}
