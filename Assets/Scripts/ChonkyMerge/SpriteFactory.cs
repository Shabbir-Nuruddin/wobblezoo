using UnityEngine;

namespace ChonkyMerge
{
    /// <summary>
    /// Builds a soft, glossy circle sprite at runtime so the prototype needs no
    /// imported art. The circle is white (tinted per tier by the SpriteRenderer)
    /// with a lighter highlight in the top-left for a cute, "bubble" shine.
    /// </summary>
    public static class SpriteFactory
    {
        private static Sprite _cached;

        public static Sprite Circle()
        {
            if (_cached != null) return _cached;

            const int size = 256;
            const float r = size * 0.5f;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var center = new Vector2(r, r);
            var highlight = new Vector2(r * 0.66f, r * 1.34f); // top-left shine

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    float edge = r - 2f;
                    // Anti-aliased circle edge.
                    float alpha = Mathf.Clamp01((edge - dist) / 2.5f);

                    // Base body a touch off-white so tint reads as a soft pastel.
                    float shade = Mathf.Lerp(0.86f, 1f, Mathf.Clamp01(1f - dist / r));

                    // Glossy highlight blob.
                    float h = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(x, y), highlight) / (r * 0.55f));
                    shade = Mathf.Lerp(shade, 1f, h * 0.9f);

                    byte c = (byte)(Mathf.Clamp01(shade) * 255f);
                    pixels[y * size + x] = new Color32(c, c, c, (byte)(alpha * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();

            _cached = Sprite.Create(tex, new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f), size); // 1 world unit diameter
            _cached.name = "CritterCircle";
            return _cached;
        }
    }
}
