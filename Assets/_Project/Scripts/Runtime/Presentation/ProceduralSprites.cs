using UnityEngine;
using UnityEngine.UI;

namespace CozyLab.Puzzle.Presentation
{
    /// <summary>
    /// Generates the few soft shapes the prototype needs at runtime so no art assets are required.
    /// All sprites are white; tint them with Image.color.
    /// </summary>
    public static class ProceduralSprites
    {
        // Sprite pixels-per-unit matches the Canvas default (100), so 1 sprite pixel = 1 UI unit before multipliers.
        private const float PixelsPerUnit = 100f;

        private const int RoundedTextureSize = 128;
        private const int RoundedRadiusPx = 48;

        private const int ShadowTextureSize = 160;
        private const int ShadowRadiusPx = 32;
        private const int ShadowBlurPx = 40;

        private const int CircleTextureSize = 128;

        private static Sprite _roundedRect;
        private static Sprite _softShadow;
        private static Sprite _circle;
        private static Sprite _softGlow;
        private static Sprite _ring;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            // Supports "Enter Play Mode" without domain reload.
            _roundedRect = null;
            _softShadow = null;
            _circle = null;
            _softGlow = null;
            _ring = null;
        }

        /// <summary>9-sliced rounded rectangle. Use <see cref="ApplyRounded"/> to pick the corner radius.</summary>
        public static Sprite RoundedRect => _roundedRect != null ? _roundedRect : (_roundedRect = CreateRoundedRect());

        /// <summary>9-sliced blurred rounded rectangle. The opaque core sits <c>blur</c> units inside the rect.</summary>
        public static Sprite SoftShadow => _softShadow != null ? _softShadow : (_softShadow = CreateSoftShadow());

        /// <summary>Anti-aliased solid circle.</summary>
        public static Sprite Circle => _circle != null ? _circle : (_circle = CreateCircle());

        /// <summary>Radial gradient: opaque centre fading to transparent edge.</summary>
        public static Sprite SoftGlow => _softGlow != null ? _softGlow : (_softGlow = CreateSoftGlow());

        /// <summary>Soap-bubble style ring: crisp rim with a faint filled centre.</summary>
        public static Sprite Ring => _ring != null ? _ring : (_ring = CreateRing());

        /// <summary>Configures an Image as a rounded rectangle with the given corner radius in UI units.</summary>
        public static void ApplyRounded(Image image, float cornerRadius)
        {
            image.sprite = RoundedRect;
            image.type = Image.Type.Sliced;
            image.fillCenter = true;
            image.pixelsPerUnitMultiplier = RoundedRadiusPx / Mathf.Max(cornerRadius, 0.5f);
        }

        /// <summary>
        /// Configures an Image as a soft shadow. Size the rect as (casting shape + 2 * blur).
        /// The shadow sprite's corner = radius + blur, so blur is tied to the corner size.
        /// </summary>
        public static void ApplySoftShadow(Image image, float blur)
        {
            image.sprite = SoftShadow;
            image.type = Image.Type.Sliced;
            image.fillCenter = true;
            image.pixelsPerUnitMultiplier = ShadowBlurPx / Mathf.Max(blur, 0.5f);
        }

        /// <summary>How much larger (per side) a shadow rect must be for the given blur.</summary>
        public static float ShadowPadding(float blur) => blur;

        private static Sprite CreateRoundedRect()
        {
            int size = RoundedTextureSize;
            float half = size * 0.5f;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = RoundedBoxDistance(new Vector2(x + 0.5f - half, y + 0.5f - half), new Vector2(half, half), RoundedRadiusPx);
                byte a = (byte)(Mathf.Clamp01(0.5f - d) * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, a);
            }
            return CreateSprite("CozyLab_RoundedRect", size, pixels, RoundedRadiusPx);
        }

        private static Sprite CreateSoftShadow()
        {
            int size = ShadowTextureSize;
            float half = size * 0.5f;
            var core = new Vector2(half - ShadowBlurPx, half - ShadowBlurPx);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = RoundedBoxDistance(new Vector2(x + 0.5f - half, y + 0.5f - half), core, ShadowRadiusPx);
                // d <= 0 inside the core; fade out across the blur band.
                float t = Mathf.Clamp01(d / ShadowBlurPx);
                float a = 1f - t * t * (3f - 2f * t);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * a * 255f));
            }
            return CreateSprite("CozyLab_SoftShadow", size, pixels, ShadowRadiusPx + ShadowBlurPx);
        }

        private static Sprite CreateCircle()
        {
            int size = CircleTextureSize;
            float half = size * 0.5f;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = new Vector2(x + 0.5f - half, y + 0.5f - half).magnitude - (half - 1f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(0.5f - d) * 255f));
            }
            return CreateSprite("CozyLab_Circle", size, pixels, 0);
        }

        private static Sprite CreateSoftGlow()
        {
            int size = CircleTextureSize;
            float half = size * 0.5f;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float r = new Vector2(x + 0.5f - half, y + 0.5f - half).magnitude / half;
                float a = Mathf.Clamp01(1f - r);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * a * 255f));
            }
            return CreateSprite("CozyLab_SoftGlow", size, pixels, 0);
        }

        private static Sprite CreateRing()
        {
            int size = CircleTextureSize;
            float half = size * 0.5f;
            float outer = half - 1f;
            float thickness = size * 0.07f;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float r = new Vector2(x + 0.5f - half, y + 0.5f - half).magnitude;
                float rim = Mathf.Clamp01(0.5f - Mathf.Abs(r - (outer - thickness * 0.5f)) + thickness * 0.5f);
                float fill = r < outer ? 0.12f : 0f;
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Max(rim, fill) * 255f));
            }
            return CreateSprite("CozyLab_Ring", size, pixels, 0);
        }

        /// <summary>Signed distance from point p to a rounded box centred at the origin.</summary>
        private static float RoundedBoxDistance(Vector2 p, Vector2 halfExtents, float radius)
        {
            var q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - halfExtents + new Vector2(radius, radius);
            var outside = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f));
            return outside.magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;
        }

        private static Sprite CreateSprite(string name, int size, Color32[] pixels, float border)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), PixelsPerUnit, 0,
                SpriteMeshType.FullRect, new Vector4(border, border, border, border));
            sprite.name = name;
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }
    }
}
