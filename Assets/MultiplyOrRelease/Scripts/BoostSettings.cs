using System;
using UnityEngine;

namespace MultiplyOrRelease
{
    public enum BoostKind { FireRate, DoubleAmmo, ExtraMarble }

    [Serializable]
    public sealed class BoostAppearance
    {
        [Tooltip("Allow this boost type to spawn. Keeps its Spawn Weight when toggled off. Apply / Restart after changing.")]
        public bool enabled = true;
        [Tooltip("Relative spawn chance. 0 disables this type.")]
        [Min(0)] public float spawnWeight = 1;
        [Tooltip("Leave empty for a circle. Apply / Restart after changing.")]
        public Sprite sprite;
        public Color color = Color.white;
        public string label;
        public float EffectiveSpawnWeight => enabled ? spawnWeight : 0;
    }

    [Serializable]
    public sealed class BoostSettings
    {
        public bool enabled = true;
        [Tooltip("All boost timers use simulation seconds: Pause freezes them; Speed scales them.")]
        [Min(0)] public float firstSpawnDelay = 3;
        [Min(.1f)] public float spawnIntervalMin = 5;
        [Min(.1f)] public float spawnIntervalMax = 10;
        [Tooltip("Prefer valid grid cells owned by the living team with the least territory. Equal teams are chosen randomly.")]
        public bool preferSmallerTerritories = true;
        [Tooltip("Scale spawn intervals and the remaining spawn wait when exactly two teams are alive. 0.5 halves the wait; 1 keeps normal timing.")]
        [Range(.1f, 1)] public float twoTeamSpawnIntervalMultiplier = .5f;
        [Range(1, 30)] public int maxActive = 3;
        [Min(.1f)] public float pickupLifetime = 20;
        [Min(.03f)] public float radius = .22f;
        [Min(0)] public float edgeInset = .3f;
        [Tooltip("Extra space between pickups and cannon hit circles.")]
        [Min(0)] public float cannonClearance = .3f;
        [Tooltip("Consume the bullet that collects the boost.")]
        public bool consumeProjectile = true;
        [Header("Effects")]
        [Range(1, 10)] public float fireRateMultiplier = 2;
        [Min(.1f)] public float fireRateDuration = 30;
        [Min(.1f)] public float extraMarbleDuration = 30;
        [Tooltip("Each pickup adds one Plinko marble with its own expiry. Limit extra marbles per team.")]
        [Range(1, 30)] public int maxExtraMarbles = 5;
        [Header("Collection Flight")]
        public bool animateCollection = true;
        [Tooltip("Simulation seconds to fly from the hit position to the collecting team's cannon. Pause/Speed/Step apply. The boost is awarded on arrival; timed effects start then.")]
        [Min(.05f)] public float collectionFlightDuration = 1.2f;
        [Tooltip("Scale at arrival relative to the pickup's normal size. 1 keeps the size unchanged.")]
        [Range(0, 1)] public float collectionEndScale = .25f;
        public bool fadeOnArrival = true;
        [Header("Appearance")]
        public float visualScale = 1;
        [Tooltip("Circular border around the boost. Set width to 0 to hide it.")]
        [Min(0)] public float outlineWidth = .04f;
        public Color outlineColor = Color.white;
        public int sortingOrder = 16;
        public bool showLabels = true;
        public Color labelColor = Color.black;
        [Min(.05f)] public float labelSize = .14f;
        public BoostAppearance fireRate = new BoostAppearance { color = new Color(1, .7f, .1f), label = "SPD" };
        public BoostAppearance doubleAmmo = new BoostAppearance { color = new Color(.25f, 1, .4f), label = "x2" };
        public BoostAppearance extraMarble = new BoostAppearance { color = new Color(.3f, .8f, 1), label = "+1" };

        public BoostAppearance Appearance(BoostKind kind)
            => kind == BoostKind.FireRate ? fireRate : kind == BoostKind.DoubleAmmo ? doubleAmmo : extraMarble;

        public void Validate(float boardSize)
        {
            firstSpawnDelay = Mathf.Max(0, firstSpawnDelay);
            spawnIntervalMin = Mathf.Max(.1f, spawnIntervalMin);
            spawnIntervalMax = Mathf.Max(spawnIntervalMin, spawnIntervalMax);
            twoTeamSpawnIntervalMultiplier = Mathf.Clamp(twoTeamSpawnIntervalMultiplier, .1f, 1);
            maxActive = Mathf.Clamp(maxActive, 1, 30);
            pickupLifetime = Mathf.Max(.1f, pickupLifetime);
            radius = Mathf.Clamp(radius, .03f, Mathf.Max(.03f, boardSize * .1f));
            edgeInset = Mathf.Clamp(edgeInset, 0, Mathf.Max(0, boardSize * .5f - radius));
            cannonClearance = Mathf.Max(0, cannonClearance);
            fireRateMultiplier = Mathf.Clamp(fireRateMultiplier, 1, 10);
            fireRateDuration = Mathf.Max(.1f, fireRateDuration);
            extraMarbleDuration = Mathf.Max(.1f, extraMarbleDuration);
            maxExtraMarbles = Mathf.Clamp(maxExtraMarbles, 1, 30);
            collectionFlightDuration = Mathf.Clamp(collectionFlightDuration, .05f, 10);
            collectionEndScale = Mathf.Clamp01(collectionEndScale);
            visualScale = Mathf.Clamp(visualScale, .1f, 10);
            outlineWidth = Mathf.Clamp(outlineWidth, 0, .25f);
            sortingOrder = Mathf.Clamp(sortingOrder, -32768, 32766);
            labelSize = Mathf.Max(.05f, labelSize);
            if (fireRate == null) fireRate = new BoostAppearance { label = "SPD", color = new Color(1, .7f, .1f) };
            if (doubleAmmo == null) doubleAmmo = new BoostAppearance { label = "x2", color = new Color(.25f, 1, .4f) };
            if (extraMarble == null) extraMarble = new BoostAppearance { label = "+1", color = new Color(.3f, .8f, 1) };
            fireRate.spawnWeight = Mathf.Clamp(fireRate.spawnWeight, 0, 1000);
            doubleAmmo.spawnWeight = Mathf.Clamp(doubleAmmo.spawnWeight, 0, 1000);
            extraMarble.spawnWeight = Mathf.Clamp(extraMarble.spawnWeight, 0, 1000);
        }
    }
}
