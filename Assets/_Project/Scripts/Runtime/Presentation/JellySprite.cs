using System.Collections.Generic;
using CozyLab.Puzzle.Core;
using UnityEngine;

namespace CozyLab.Puzzle.Presentation
{
    /// <summary>
    /// A single translucent surface for a whole polyomino. Shading the union avoids opaque
    /// cell/bridge seams and keeps the silhouette readable. Generated only on first use of
    /// an orientation; PieceView owns and disposes its cached textures.
    /// </summary>
    public sealed class JellySprite
    {
        public Sprite Body { get; private set; }
        public Sprite Shadow { get; private set; }
        public Vector2 SizeInCells { get; private set; }

        private struct RoundedBox
        {
            public Vector2 Center, Half;
            public float Radius;
        }

        public static JellySprite Create(PolyominoShape shape, Color color, float bodyScale, float cornerRadius)
        {
            const int pixelsPerCell = 80;
            const float margin = 0.2f;
            int width = Mathf.CeilToInt((shape.Width + margin * 2f) * pixelsPerCell);
            int height = Mathf.CeilToInt((shape.Height + margin * 2f) * pixelsPerCell);
            var boxes = new List<RoundedBox>();
            float half = bodyScale * 0.5f;
            float bridge = bodyScale * 0.78f;
            foreach (var cell in shape.Cells)
            {
                var center = new Vector2(cell.x + 0.5f, shape.Height - cell.y - 0.5f);
                boxes.Add(new RoundedBox { Center = center, Half = Vector2.one * half, Radius = bodyScale * cornerRadius });
                if (shape.Contains(cell + Vector2Int.right))
                    boxes.Add(new RoundedBox { Center = center + Vector2.right * 0.5f, Half = new Vector2(0.5f, bridge * 0.5f), Radius = 0.14f });
                if (shape.Contains(cell + Vector2Int.up))
                    boxes.Add(new RoundedBox { Center = center + Vector2.down * 0.5f, Half = new Vector2(bridge * 0.5f, 0.5f), Radius = 0.14f });
                if (shape.Contains(cell + Vector2Int.right) && shape.Contains(cell + Vector2Int.up) &&
                    shape.Contains(cell + Vector2Int.one))
                    boxes.Add(new RoundedBox { Center = center + new Vector2(0.5f, -0.5f), Half = Vector2.one * half, Radius = 0.12f });
            }

            var distances = new float[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                var p = new Vector2((x + 0.5f) / pixelsPerCell - margin, (y + 0.5f) / pixelsPerCell - margin);
                float distance = 100f;
                foreach (var box in boxes)
                {
                    var q = new Vector2(Mathf.Abs(p.x - box.Center.x), Mathf.Abs(p.y - box.Center.y)) - box.Half + Vector2.one * box.Radius;
                    float d = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - box.Radius;
                    // Small smooth union rounds the cell/bridge junction without changing the grid footprint.
                    float h = Mathf.Max(0.07f - Mathf.Abs(d - distance), 0f) / 0.07f;
                    distance = Mathf.Min(distance, d) - h * h * 0.0175f;
                }
                distances[y * width + x] = distance;
            }

            var body = new Color32[width * height];
            var shadow = new Color32[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int index = y * width + x;
                float d = distances[index];
                float soft = 1f - Mathf.SmoothStep(0f, 1f, (d + 0.025f) / 0.15f);
                shadow[index] = new Color(1f, 1f, 1f, soft * soft);
                float coverage = Mathf.Clamp01(0.5f - d * pixelsPerCell);
                if (coverage <= 0f) continue;
                float depth = Mathf.Max(0f, -d);
                var normal = new Vector2(
                    distances[y * width + Mathf.Min(x + 1, width - 1)] - distances[y * width + Mathf.Max(x - 1, 0)],
                    distances[Mathf.Min(y + 1, height - 1) * width + x] - distances[Mathf.Max(y - 1, 0) * width + x]).normalized;
                float light = Vector2.Dot(normal, new Vector2(-0.6f, 0.8f));
                float edge = Mathf.Exp(-depth / 0.085f);
                float volume = 1f - Mathf.Exp(-depth / 0.15f);
                var tint = color * (0.69f + volume * 0.29f + Mathf.Max(0f, light) * edge * 0.12f);
                float specular = Mathf.Pow(Mathf.Max(0f, light), 5f) * Mathf.Exp(-Mathf.Pow((depth - 0.045f) / 0.025f, 2f)) * 0.6f;
                float px = (x + 0.5f) / pixelsPerCell - margin;
                float py = (y + 0.5f) / pixelsPerCell - margin;
                float softHighlight = 0f;
                foreach (var cell in shape.Cells)
                {
                    float dx = (px - (cell.x + 0.34f)) / 0.27f;
                    float dy = (py - (shape.Height - cell.y - 0.28f)) / 0.12f;
                    softHighlight = Mathf.Max(softHighlight, Mathf.Exp(-(dx * dx + dy * dy) * 2f) * 0.32f);
                }
                tint = Color.Lerp(tint, new Color(1f, 0.97f, 0.86f), Mathf.Clamp01(specular + softHighlight));
                // Thin transmitted light on the lower edge, rather than a flat white core.
                tint = Color.Lerp(tint, color, Mathf.Max(0f, -light) * edge * 0.4f);
                tint.a = coverage * Mathf.Lerp(0.96f, 0.87f, volume);
                body[index] = tint;
            }
            return new JellySprite {
                Body = MakeSprite("JellySurface", width, height, body),
                Shadow = MakeSprite("JellyContact", width, height, shadow),
                SizeInCells = new Vector2(width, height) / pixelsPerCell
            };
        }

        private static Sprite MakeSprite(string name, int width, int height, Color32[] pixels)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) {
                name = name, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0, 0, width, height), Vector2.one * 0.5f, 100f, 0, SpriteMeshType.FullRect);
            sprite.name = name;
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }

        public void Dispose()
        {
            if (Body != null) { Object.Destroy(Body.texture); Object.Destroy(Body); }
            if (Shadow != null) { Object.Destroy(Shadow.texture); Object.Destroy(Shadow); }
        }
    }
}
