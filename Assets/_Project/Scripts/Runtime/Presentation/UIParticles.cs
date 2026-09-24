using UnityEngine;
using UnityEngine.UI;

namespace CozyLab.Puzzle.Presentation
{
    /// <summary>
    /// Tiny pooled particle emitter for uGUI (Unity ParticleSystems don't render inside an overlay canvas).
    /// All particles are pre-created Images updated from one Update loop; the component sleeps when idle.
    /// </summary>
    public sealed class UIParticles : MonoBehaviour
    {
        private struct Particle
        {
            public Image Image;
            public RectTransform Rect;
            public Vector2 Position;
            public Vector2 Velocity;
            public float Age;
            public float Life;
            public float StartSize;
            public float EndSize;
            public float Buoyancy;
            public Color Color;
            public bool Alive;
        }

        private const float Drag = 3.2f;

        private Particle[] _particles;
        private int _next;
        private int _aliveCount;

        public void Initialize(int capacity)
        {
            _particles = new Particle[capacity];
            var root = (RectTransform)transform;
            for (int i = 0; i < capacity; i++)
            {
                var image = UIFactory.CreateSpriteImage("P", root, ProceduralSprites.Circle, Vector2.one, Color.clear);
                image.enabled = false;
                _particles[i] = new Particle { Image = image, Rect = image.rectTransform };
            }
            enabled = false;
        }

        /// <summary>Emits one particle at a world position (converted into this layer's space).</summary>
        public void Emit(Vector3 worldPosition, Vector2 velocity, float startSize, float endSize, float life, Color color,
            Sprite sprite, float buoyancy = 0f)
        {
            if (_particles == null || _particles.Length == 0) return;

            ref var p = ref _particles[_next];
            _next = (_next + 1) % _particles.Length;
            if (!p.Alive) _aliveCount++;

            p.Position = transform.InverseTransformPoint(worldPosition);
            p.Velocity = velocity;
            p.Age = 0f;
            p.Life = Mathf.Max(0.05f, life);
            p.StartSize = startSize;
            p.EndSize = endSize;
            p.Buoyancy = buoyancy;
            p.Color = color;
            p.Alive = true;
            p.Image.sprite = sprite;
            p.Image.enabled = true;
            p.Rect.SetAsLastSibling();
            Apply(ref p, 0f);

            enabled = true;
        }

        /// <summary>Radial burst of small bubbles that drift upward.</summary>
        public void BubbleBurst(Vector3 worldPosition, Color color, int count, float speed, float size, float spread = 0f)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var offset = transform.TransformVector(dir * Random.Range(0f, spread));
                float s = size * Random.Range(0.55f, 1.1f);
                var tint = Color.Lerp(color, Color.white, Random.Range(0.25f, 0.6f));
                tint.a = Random.Range(0.6f, 0.9f);
                Emit(worldPosition + offset, dir * speed * Random.Range(0.5f, 1.1f), s, s * 1.25f,
                    Random.Range(0.45f, 0.75f), tint, i % 3 == 0 ? ProceduralSprites.Circle : ProceduralSprites.Ring,
                    buoyancy: speed * 0.35f);
            }
        }

        /// <summary>Expanding ring that fades out, e.g. under a freshly placed cell.</summary>
        public void Ripple(Vector3 worldPosition, Color color, float startSize, float endSize, float life)
        {
            Emit(worldPosition, Vector2.zero, startSize, endSize, life, color, ProceduralSprites.Ring);
        }

        public void Clear()
        {
            if (_particles == null) return;
            for (int i = 0; i < _particles.Length; i++)
            {
                _particles[i].Alive = false;
                _particles[i].Image.enabled = false;
            }
            _aliveCount = 0;
            enabled = false;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            float damping = Mathf.Exp(-Drag * dt);
            for (int i = 0; i < _particles.Length; i++)
            {
                ref var p = ref _particles[i];
                if (!p.Alive) continue;

                p.Age += dt;
                if (p.Age >= p.Life)
                {
                    p.Alive = false;
                    p.Image.enabled = false;
                    _aliveCount--;
                    continue;
                }

                p.Velocity *= damping;
                p.Velocity.y += p.Buoyancy * dt;
                p.Position += p.Velocity * dt;
                Apply(ref p, p.Age / p.Life);
            }

            if (_aliveCount <= 0)
            {
                _aliveCount = 0;
                enabled = false;
            }
        }

        private static void Apply(ref Particle p, float t)
        {
            float size = Mathf.Lerp(p.StartSize, p.EndSize, Ease.OutCubic(t));
            p.Rect.anchoredPosition = p.Position;
            p.Rect.sizeDelta = new Vector2(size, size);
            var c = p.Color;
            c.a *= (1f - t) * (1f - t * 0.5f);
            p.Image.color = c;
        }
    }
}
