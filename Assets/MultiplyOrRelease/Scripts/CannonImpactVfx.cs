using System;
using System.Collections.Generic;
using UnityEngine;

namespace MultiplyOrRelease
{
    // Pooled visual bursts live outside the cannon so lethal hits can hide the flag
    // while the explosion continues. The visual RNG never changes the simulation.
    public sealed class CannonImpactVfx : IDisposable
    {
        sealed class Spark
        {
            public SpriteRenderer renderer;
            public Vector2 direction;
            public Color color;
            public float distance, size, spin;
        }

        sealed class Explosion
        {
            public Transform pivot;
            public SpriteRenderer flash, glow;
            public LineRenderer ring;
            public readonly List<Spark> sparks = new List<Spark>();
            public readonly List<Spark> smoke = new List<Spark>();
            public float age, size, duration;
            public long sequence;
            public bool active;
        }

        readonly SimulationModel model;
        readonly CannonImpactSettings settings;
        readonly Transform root;
        readonly List<Explosion> pool = new List<Explosion>();
        readonly System.Random random = new System.Random(7319);
        long sequence;
        bool disposed;
        public int ActiveCount { get; private set; }
        public int PoolCount => pool.Count;

        public CannonImpactVfx(Transform parent, SimulationModel model)
        {
            this.model = model;
            settings = model.config.cannonImpact;
            root = new GameObject("Cannon Impact VFX").transform;
            root.SetParent(parent, false);
            model.CannonHit += Burst;
        }

        public void Burst(int target, int shooter, bool destroyed)
        {
            if (disposed || !settings.enabled || target < 0 || target >= model.teams.Length) return;
            var burst = Acquire();
            burst.age = 0;
            burst.duration = settings.duration;
            burst.size = model.config.cannon.marbleDiameter * settings.sizeMultiplier *
                (destroyed ? settings.destructionMultiplier : 1);
            burst.sequence = ++sequence;
            burst.pivot.localPosition = model.teams[target].cannonPosition;
            burst.pivot.gameObject.SetActive(true);
            if (!burst.active) { burst.active = true; ActiveCount++; }
            Color teamColor = settings.useTeamColor ? model.config.teams[target].projectileColor : settings.fireColor;
            while (burst.sparks.Count < settings.sparkCount)
                burst.sparks.Add(new Spark { renderer = Graphic("Spark", burst.pivot, false, 2) });
            float angleOffset = Range(0, Mathf.PI * 2);
            for (int i = 0; i < burst.sparks.Count; i++)
            {
                var spark = burst.sparks[i];
                spark.renderer.gameObject.SetActive(i < settings.sparkCount);
                if (i >= settings.sparkCount) continue;
                float angle = angleOffset + (i + Range(-.25f, .25f)) * Mathf.PI * 2 / settings.sparkCount;
                spark.direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                spark.distance = burst.size * settings.sparkSpread * Range(.55f, 1);
                spark.size = burst.size * Range(.035f, .065f);
                spark.spin = Range(-220, 220);
                spark.color = i % 3 == 0 ? teamColor : Color.Lerp(settings.fireColor, Color.white, Range(.15f, .65f));
                spark.renderer.sortingOrder = settings.sortingOrder + 2;
            }
            foreach (var puff in burst.smoke)
            {
                float angle = Range(0, Mathf.PI * 2);
                puff.direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                puff.distance = burst.size * Range(.25f, .55f);
                puff.size = burst.size * Range(.35f, .65f);
                puff.renderer.sortingOrder = settings.sortingOrder;
            }
            burst.glow.sortingOrder = settings.sortingOrder + 1;
            burst.ring.sortingOrder = settings.sortingOrder + 1;
            burst.flash.sortingOrder = settings.sortingOrder + 3;
            Draw(burst);
        }

        Explosion Acquire()
        {
            foreach (var burst in pool) if (!burst.active) return burst;
            if (pool.Count >= settings.maxBursts)
            {
                var oldest = pool[0];
                foreach (var burst in pool) if (burst.sequence < oldest.sequence) oldest = burst;
                return oldest;
            }
            var result = new Explosion();
            result.pivot = new GameObject("Cannon Explosion").transform;
            result.pivot.SetParent(root, false);
            result.glow = Graphic("Explosion Glow", result.pivot, true, 1);
            result.flash = Graphic("Explosion Flash", result.pivot, true, 3);
            for (int i = 0; i < 5; i++)
                result.smoke.Add(new Spark { renderer = Graphic("Smoke", result.pivot, true, 0) });
            var ringObject = new GameObject("Shockwave", typeof(LineRenderer));
            ringObject.transform.SetParent(result.pivot, false);
            result.ring = ringObject.GetComponent<LineRenderer>();
            result.ring.sharedMaterial = model.config.presentation.trailMaterial;
            result.ring.useWorldSpace = false;
            result.ring.loop = true;
            result.ring.positionCount = 48;
            result.ring.numCornerVertices = 2;
            for (int i = 0; i < 48; i++)
            {
                float angle = i * Mathf.PI * 2 / 48;
                result.ring.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0));
            }
            pool.Add(result);
            return result;
        }

        SpriteRenderer Graphic(string name, Transform parent, bool circle, int layer)
        {
            var go = new GameObject(name, typeof(SpriteRenderer));
            go.transform.SetParent(parent, false);
            var renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = circle ? model.config.presentation.circleSprite : model.config.presentation.squareSprite;
            renderer.sharedMaterial = model.config.presentation.spriteMaterial;
            renderer.sortingOrder = settings.sortingOrder + layer;
            return renderer;
        }

        public void Advance(float deltaTime)
        {
            if (disposed || deltaTime <= 0) return;
            foreach (var burst in pool)
            {
                if (!burst.active) continue;
                burst.age += deltaTime;
                if (burst.age >= burst.duration)
                {
                    burst.active = false;
                    ActiveCount--;
                    burst.pivot.gameObject.SetActive(false);
                }
                else Draw(burst);
            }
        }

        void Draw(Explosion burst)
        {
            float t = Mathf.Clamp01(burst.age / burst.duration);
            float expand = 1 - (1 - t) * (1 - t);
            float flashFade = 1 - Mathf.Clamp01(t / .24f);
            SetGraphic(burst.flash, Vector2.zero, Vector2.one * burst.size * Mathf.Lerp(.75f, 1.15f, expand),
                Color.Lerp(new Color(1, 1, .8f), settings.fireColor, t), flashFade);
            SetGraphic(burst.glow, Vector2.zero, Vector2.one * burst.size * Mathf.Lerp(1.1f, 1.65f, expand),
                settings.fireColor, .28f * (1 - Mathf.Clamp01(t / .55f)));
            float ringSize = burst.size * Mathf.Lerp(.2f, 1.15f, expand);
            burst.ring.transform.localScale = Vector3.one * ringSize;
            burst.ring.widthMultiplier = burst.size * .055f / ringSize;
            var ringColor = Color.Lerp(new Color(1, .95f, .6f), settings.fireColor, t);
            ringColor.a = settings.fireColor.a * (1 - Mathf.SmoothStep(0, 1, t));
            burst.ring.startColor = burst.ring.endColor = ringColor;
            foreach (var spark in burst.sparks)
            {
                if (!spark.renderer.gameObject.activeSelf) continue;
                Vector2 position = spark.direction * spark.distance * expand;
                position.y -= burst.size * .25f * t * t;
                float fade = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.2f, 1, t));
                float size = spark.size * Mathf.Lerp(1, .25f, t);
                SetGraphic(spark.renderer, position, new Vector2(size * 2.8f, size), spark.color, fade);
                float angle = Mathf.Atan2(spark.direction.y, spark.direction.x) * Mathf.Rad2Deg + spark.spin * t;
                spark.renderer.transform.localRotation = Quaternion.Euler(0, 0, angle);
            }
            foreach (var puff in burst.smoke)
            {
                Vector2 position = puff.direction * puff.distance * expand + Vector2.up * burst.size * t * .25f;
                float opacity = settings.smokeOpacity * Mathf.Sin(t * Mathf.PI);
                SetGraphic(puff.renderer, position, Vector2.one * puff.size * Mathf.Lerp(.35f, 1.4f, expand),
                    settings.smokeColor, opacity);
            }
        }

        static void SetGraphic(SpriteRenderer renderer, Vector2 position, Vector2 size, Color color, float opacity)
        {
            Vector3 bounds = renderer.sprite.bounds.size;
            renderer.transform.localScale = new Vector3(size.x / bounds.x, size.y / bounds.y, 1);
            // VFX primitives are centred regardless of their source sprite pivot.
            renderer.transform.localPosition = (Vector3)position -
                renderer.transform.localRotation * Vector3.Scale(renderer.sprite.bounds.center, renderer.transform.localScale);
            color.a *= opacity;
            renderer.color = color;
        }

        float Range(float min, float max) => min + (max - min) * (float)random.NextDouble();

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            model.CannonHit -= Burst;
            ActiveCount = 0;
            root.gameObject.SetActive(false);
            if (Application.isPlaying) UnityEngine.Object.Destroy(root.gameObject);
            else UnityEngine.Object.DestroyImmediate(root.gameObject);
        }
    }
}
