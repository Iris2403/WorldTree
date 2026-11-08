using System.Collections.Generic;
using UnityEngine;

namespace WorldTree.Game
{
    /// <summary>
    /// Generated stand-in art (roadmap D42: stand-ins now, real art later). Every shape is white with soft
    /// top-left shading baked in, so a tint gives it the chain colour while keeping depth. Real icons
    /// assigned in the data assets replace these without code changes (see docs/art/asset-spec.md).
    /// </summary>
    public static class PlaceholderArt
    {
        public enum Shape
        {
            Circle,
            Leaf,
            Drop,
            Crate,
            Star,
        }

        private const int Size = 128;
        private static readonly Dictionary<Shape, Sprite> Shapes = new Dictionary<Shape, Sprite>();
        private static Sprite _cell;
        private static Sprite _frame;
        private static Sprite _panel;
        private static Sprite _dot;

        /// <summary>Rounded square used for board cells and covers.</summary>
        public static Sprite Cell => _cell ? _cell : _cell = Make(Size, (x, y) => Fill(SignedBox(x, y, 0.47f, 0.22f)), 0f);

        /// <summary>Thick rounded frame used for generators, so they read differently from items.</summary>
        public static Sprite Frame => _frame ? _frame : _frame = Make(Size, (x, y) =>
            Fill(SignedBox(x, y, 0.47f, 0.28f)) * (1f - Fill(SignedBox(x, y, 0.31f, 0.14f))), 0.25f);

        /// <summary>Small soft dot, used for particles and level pips.</summary>
        public static Sprite Dot => _dot ? _dot : _dot = Make(32, (x, y) =>
        {
            float d = Mathf.Sqrt(x * x + y * y);
            return Mathf.Clamp01(1f - d * 2f);
        }, 0f);

        /// <summary>Rounded panel with 9-slice borders for UI panels and buttons.</summary>
        public static Sprite Panel
        {
            get
            {
                if (_panel) return _panel;
                const int size = 64;
                var px = Pixels(size, (x, y) => Fill(SignedBox(x, y, 0.47f, 0.25f) * size / 2f + 0.5f, true), 0.12f);
                var texture = NewTexture(size, px);
                _panel = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                    SpriteMeshType.FullRect, new Vector4(20, 20, 20, 20));
                return _panel;
            }
        }

        public static Sprite ItemShape(Shape shape)
        {
            if (Shapes.TryGetValue(shape, out var sprite) && sprite) return sprite;
            sprite = Make(Size, ShapeFunction(shape), 0.3f);
            Shapes[shape] = sprite;
            return sprite;
        }

        /// <summary>Stand-in shape per chain: leaf (Nature), drop (Water), crate (Craft), round treat (Food).</summary>
        public static Shape ShapeForChain(string chainId)
        {
            if (chainId == null) return Shape.Circle;
            if (chainId.EndsWith(".nature")) return Shape.Leaf;
            if (chainId.EndsWith(".water")) return Shape.Drop;
            if (chainId.EndsWith(".craft")) return Shape.Crate;
            if (chainId.EndsWith("mystery_acorn")) return Shape.Star;
            return Shape.Circle;
        }

        private delegate float Field(float x, float y);

        private static Field ShapeFunction(Shape shape)
        {
            switch (shape)
            {
                case Shape.Leaf:
                    // Two overlapping discs make a pointed leaf, tilted 45 degrees.
                    return (x, y) =>
                    {
                        float u = (x + y) * 0.7071f, v = (x - y) * 0.7071f;
                        float a = Disc(u, v - 0.2f, 0.42f), b = Disc(u, v + 0.2f, 0.42f);
                        return Mathf.Min(a, b);
                    };
                case Shape.Drop:
                    // A disc with a pointed top.
                    return (x, y) =>
                    {
                        float circle = Disc(x, y + 0.1f, 0.33f);
                        float tip = y > -0.1f ? Mathf.Clamp01(1f - (Mathf.Abs(x) - (0.33f - (y + 0.1f) * 0.62f)) * 64f) : 0f;
                        return Mathf.Max(circle, y > -0.1f && y < 0.43f ? tip : 0f);
                    };
                case Shape.Crate:
                    return (x, y) => Fill(SignedBox(x, y, 0.38f, 0.1f)) * (1f - 0.35f * Fill(SignedBox(x, y, 0.24f, 0.05f)));
                case Shape.Star:
                    return (x, y) =>
                    {
                        float r = Mathf.Sqrt(x * x + y * y), angle = Mathf.Atan2(y, x);
                        float edge = 0.28f + 0.14f * Mathf.Cos(angle * 5f);
                        return Mathf.Clamp01((edge - r) * 64f);
                    };
                default:
                    return (x, y) => Disc(x, y, 0.42f);
            }
        }

        private static float Disc(float x, float y, float radius) => Mathf.Clamp01((radius - Mathf.Sqrt(x * x + y * y)) * 64f);

        private static float Fill(float signedDistance, bool pixelUnits = false) =>
            pixelUnits ? Mathf.Clamp01(-signedDistance + 0.5f) : Mathf.Clamp01(-signedDistance * 64f + 0.5f);

        /// <summary>Signed distance to a rounded box centred at the origin (coordinates and sizes as fractions of the texture).</summary>
        private static float SignedBox(float x, float y, float half, float radius)
        {
            float qx = Mathf.Abs(x) - (half - radius), qy = Mathf.Abs(y) - (half - radius);
            float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
        }

        private static Sprite Make(int size, Field alpha, float shading) =>
            Sprite.Create(NewTexture(size, Pixels(size, alpha, shading)), new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);

        /// <param name="shading">0 = flat white; higher = darker towards the bottom-right (light from the top-left).</param>
        private static Color32[] Pixels(int size, Field alpha, float shading)
        {
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float fx = (x + 0.5f) / size - 0.5f, fy = (y + 0.5f) / size - 0.5f;
                float a = alpha(fx, fy);
                float light = 1f - shading * Mathf.Clamp01(0.5f + fx - fy);
                byte c = (byte)(255 * light);
                px[y * size + x] = new Color32(c, c, c, (byte)(255 * Mathf.Clamp01(a)));
            }
            return px;
        }

        private static Texture2D NewTexture(int size, Color32[] pixels)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "WorldTreePlaceholder",
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
