using System;
using UnityEngine;

namespace MultiplyOrRelease
{
    [Serializable] public sealed class CannonImpactSettings
    {
        public bool enabled = true;
        [Tooltip("Simulation seconds. Pause freezes the explosion; Speed and Step advance it.")]
        [Range(.15f, 2)] public float duration = .7f;
        [Tooltip("Explosion size relative to the cannon flag's diameter.")]
        [Range(.5f, 4)] public float sizeMultiplier = 1.8f;
        [Tooltip("Extra size for the hit that destroys a cannon.")]
        [Range(1, 3)] public float destructionMultiplier = 1.35f;
        [Range(4, 48)] public int sparkCount = 16;
        [Range(.3f, 3)] public float sparkSpread = 1.25f;
        public Color fireColor = new Color(1, .45f, .06f, 1);
        [Tooltip("Mix in debris using the hit cannon team's Projectile Color.")]
        public bool useTeamColor = true;
        public Color smokeColor = new Color(.22f, .25f, .3f, 1);
        [Range(0, 1)] public float smokeOpacity = .32f;
        [Tooltip("Maximum pooled explosions; reuse the oldest at capacity.")]
        [Range(4, 32)] public int maxBursts = 12;
        public int sortingOrder = 40;

        public void Validate()
        {
            duration = Mathf.Clamp(duration, .15f, 2);
            sizeMultiplier = Mathf.Clamp(sizeMultiplier, .5f, 4);
            destructionMultiplier = Mathf.Clamp(destructionMultiplier, 1, 3);
            sparkCount = Mathf.Clamp(sparkCount, 4, 48);
            sparkSpread = Mathf.Clamp(sparkSpread, .3f, 3);
            smokeOpacity = Mathf.Clamp01(smokeOpacity);
            maxBursts = Mathf.Clamp(maxBursts, 4, 32);
            sortingOrder = Mathf.Clamp(sortingOrder, -32768, 32764);
        }
    }
}
