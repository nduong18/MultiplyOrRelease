using System;
using UnityEngine;

namespace MultiplyOrRelease
{
    public enum PlinkoBoostKind { MarbleSpeed, MultiplyAmmo }

    [Serializable]
    public sealed class ConquestPlinkoBoost
    {
        public bool enabled = true;
        public PlinkoBoostKind kind;
        [Tooltip("Position within the playable Plinko area: (0,0) bottom left, (1,1) top right.")]
        public Vector2 position = new Vector2(.5f, .5f);
        [Min(.05f)] public float radius = .22f;
        public Color color = new Color(.1f, .85f, 1);
        [Tooltip("After one marble collects this boost, hide it for this many simulation seconds before any marble can collect it again. Pause freezes the timer.")]
        [Min(.1f)] public float respawnDelay = 10;
        [Header("Marble Speed")]
        [Range(1, 4)] public float speedMultiplier = 1.6f;
        [Tooltip("Minimum downward speed after collection. World units per simulation second.")]
        [Min(0)] public float minimumFallSpeed = 4;
        [Min(.1f)] public float maxSpeed = 10;
        [Header("Multiply Ammo")]
        [Min(2)] public int ammoMultiplier = 2;

        public void Validate()
        {
            position.x = Mathf.Clamp01(position.x); position.y = Mathf.Clamp01(position.y);
            radius = Mathf.Clamp(radius, .05f, .5f); respawnDelay = Mathf.Max(.1f, respawnDelay);
            speedMultiplier = Mathf.Clamp(speedMultiplier, 1, 4);
            maxSpeed = Mathf.Clamp(maxSpeed, .1f, 12);
            minimumFallSpeed = Mathf.Clamp(minimumFallSpeed, 0, maxSpeed);
            ammoMultiplier = Mathf.Max(2, ammoMultiplier);
        }
    }
}
