using UnityEngine;

namespace WorldTree.Game
{
    /// <summary>
    /// Generated graybox sprites (prototype only, replaced in Phase 6). Nothing external is used,
    /// so there are no licensing questions. Sprites are white and tinted per chain by the renderer.
    /// </summary>
    public static class PlaceholderArt
    {
        private const int Size = 128;
        private static Sprite _cell;
        private static Sprite _circle;
        private static Sprite _frame;

        /// <summary>Rounded square used for board cells.</summary>
        public static Sprite Cell => _cell ? _cell : _cell = Make(RoundedSquare(0.22f, 0f));

        /// <summary>Disc used for normal items.</summary>
        public static Sprite Circle => _circle ? _circle : _circle = Make(Disc());

        /// <summary>Thick rounded frame used for generators, so they read differently from items.</summary>
        public static Sprite Frame => _frame ? _frame : _frame = Make(RoundedSquare(0.28f, 0.16f));

        private static Sprite Make(Color32[] pixels)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "WorldTreePlaceholder",
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size);
        }

        private static Color32[] Disc()
        {
            var px = new Color32[Size * Size];
            float r = Size * 0.5f - 1f;
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float dx = x + 0.5f - Size * 0.5f, dy = y + 0.5f - Size * 0.5f;
                float a = Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy));
                px[y * Size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            return px;
        }

        /// <param name="radius">Corner radius as a fraction of the size.</param>
        /// <param name="hole">0 for a filled square; otherwise the border thickness as a fraction of the size.</param>
        private static Color32[] RoundedSquare(float radius, float hole)
        {
            var px = new Color32[Size * Size];
            float half = Size * 0.5f - 1f;
            float rad = radius * Size;
            float innerHalf = half - hole * Size;
            float innerRad = Mathf.Max(0f, rad - hole * Size);
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float dx = Mathf.Abs(x + 0.5f - Size * 0.5f), dy = Mathf.Abs(y + 0.5f - Size * 0.5f);
                float outer = Mathf.Clamp01(-SignedBox(dx, dy, half, rad) + 0.5f);
                float alpha = outer;
                if (hole > 0f) alpha *= Mathf.Clamp01(SignedBox(dx, dy, innerHalf, innerRad) + 0.5f);
                px[y * Size + x] = new Color32(255, 255, 255, (byte)(alpha * 255));
            }
            return px;
        }

        // Signed distance to a rounded box centred at the origin (negative inside).
        private static float SignedBox(float px, float py, float half, float radius)
        {
            float qx = px - (half - radius), qy = py - (half - radius);
            float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
        }
    }
}
