using System;
using System.Collections.Generic;
using UnityEngine;

namespace MultiplyOrRelease
{
    // Round, trail-free bubbles with a faint halo, recycled across grid impacts.
    // Visual randomness is isolated from both gameplay RNG and Unity's global RNG.
    public sealed class GridImpactVfx : IDisposable
    {
        sealed class Bubble
        {
            public Transform pivot;
            public SpriteRenderer core, halo;
            public Vector2 origin, direction;
            public Color color;
            public float age, diameter, travel;
            public long sequence;
            public bool active;
        }

        readonly SimulationModel model;
        readonly GridImpactSettings settings;
        readonly Transform root;
        readonly List<Bubble> pool = new List<Bubble>();
        readonly System.Random random = new System.Random(913);
        long sequence;
        bool disposed;
        public int ActiveCount { get; private set; }
        public int PoolCount => pool.Count;

        public GridImpactVfx(Transform parent, SimulationModel model)
        {
            this.model = model; settings = model.config.gridImpact;
            root = new GameObject("Grid Impact VFX").transform;
            root.SetParent(parent, false);
            model.GridHit += Burst;
        }

        public void Burst(Vector2 position, int team)
        {
            if (disposed || !settings.enabled || team < 0 || team >= model.config.teams.Length) return;
            float cell = Mathf.Min(model.CellWidth, model.CellHeight);
            Color tint = settings.useTeamColor ? model.config.teams[team].projectileColor : settings.color;
            var b = Acquire();
            float angle = Range(0, Mathf.PI * 2);
            b.direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            b.origin = position;
            b.age = 0; b.diameter = cell * settings.sizeInCells * Range(.85f, 1.15f);
            b.travel = cell * settings.spreadInCells * Range(.45f, 1);
            b.color = Color.Lerp(tint, Color.white, Range(0, .12f)); b.color.a = tint.a;
            b.sequence = ++sequence;
            if (!b.active) { b.active = true; ActiveCount++; }
            b.pivot.gameObject.SetActive(true);
            Draw(b);
        }

        Bubble Acquire()
        {
            foreach (var b in pool) if (!b.active) return b;
            if (pool.Count >= settings.maxParticles)
            {
                var oldest = pool[0];
                foreach (var b in pool) if (b.sequence < oldest.sequence) oldest = b;
                return oldest;
            }
            var bubble = new Bubble();
            bubble.pivot = new GameObject("Impact Bubble").transform;
            bubble.pivot.SetParent(root, false);
            bubble.halo = Graphic("Halo", bubble.pivot, settings.sortingOrder - 1);
            bubble.core = Graphic("Circle", bubble.pivot, settings.sortingOrder);
            pool.Add(bubble);
            return bubble;
        }

        SpriteRenderer Graphic(string name, Transform parent, int order)
        {
            var go = new GameObject(name, typeof(SpriteRenderer)); go.transform.SetParent(parent, false);
            var sprite = go.GetComponent<SpriteRenderer>();
            sprite.sprite = model.config.presentation.circleSprite;
            sprite.sharedMaterial = model.config.presentation.spriteMaterial;
            sprite.sortingOrder = order;
            return sprite;
        }

        public void Advance(float deltaTime)
        {
            if (disposed || deltaTime <= 0) return;
            foreach (var b in pool)
            {
                if (!b.active) continue;
                b.age += deltaTime;
                if (b.age >= settings.duration)
                {
                    b.active = false; ActiveCount--; b.pivot.gameObject.SetActive(false);
                }
                else Draw(b);
            }
        }

        void Draw(Bubble b)
        {
            float t = Mathf.Clamp01(b.age / settings.duration);
            float expand = 1 - (1 - t) * (1 - t);
            b.pivot.localPosition = b.origin + b.direction * b.travel * expand;
            float fade = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.35f, 1, t));
            float size = b.diameter * Mathf.Lerp(1, .35f, t);
            SetGraphic(b.core, size, b.color, settings.opacity * fade);
            SetGraphic(b.halo, size * 1.65f, b.color, settings.haloOpacity * fade);
        }

        static void SetGraphic(SpriteRenderer renderer, float diameter, Color color, float opacity)
        {
            Vector3 bounds = renderer.sprite.bounds.size;
            renderer.transform.localScale = new Vector3(diameter / bounds.x, diameter / bounds.y, 1);
            renderer.transform.localPosition = -Vector3.Scale(renderer.sprite.bounds.center, renderer.transform.localScale);
            color.a *= opacity; renderer.color = color;
        }

        float Range(float min, float max) => min + (max - min) * (float)random.NextDouble();

        public void Dispose()
        {
            if (disposed) return;
            disposed = true; model.GridHit -= Burst; ActiveCount = 0;
            root.gameObject.SetActive(false);
            if (Application.isPlaying) UnityEngine.Object.Destroy(root.gameObject);
            else UnityEngine.Object.DestroyImmediate(root.gameObject);
        }
    }
}
