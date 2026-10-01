using System;
using UnityEngine;

namespace MultiplyOrRelease
{
    [Serializable] public sealed class GridImpactSettings
    {
        public bool enabled = true;
        [Tooltip("Use the firing team's Projectile Color; otherwise use Color below.")]
        public bool useTeamColor = true;
        public Color color = new Color(.1f, 1f, .3f, 1);
        [Tooltip("Seconds at simulation speed 1. Pause and Speed also affect this VFX.")]
        [Range(.05f, 2)] public float duration = .4f;
        [Tooltip("Circle diameter in grid-cell units; automatically follows grid resolution.")]
        [Range(.1f, 5)] public float sizeInCells = 1.3f;
        [Tooltip("Maximum distance travelled by the circles, in grid-cell units.")]
        [Range(0, 8)] public float spreadInCells = .5f;
        [Range(0, 1)] public float opacity = .9f;
        [Range(0, 1)] public float haloOpacity = .16f;
        public int sortingOrder = 30;
        [Tooltip("Pooled circle limit shared by all impacts; oldest circles are reused at capacity.")]
        [Range(32, 2048)] public int maxParticles = 512;

        public void Validate()
        {
            duration = Mathf.Clamp(duration, .05f, 2);
            sizeInCells = Mathf.Clamp(sizeInCells, .1f, 5);
            spreadInCells = Mathf.Clamp(spreadInCells, 0, 8);
            opacity = Mathf.Clamp01(opacity); haloOpacity = Mathf.Clamp01(haloOpacity);
            maxParticles = Mathf.Clamp(maxParticles, 32, 2048);
            sortingOrder = Mathf.Clamp(sortingOrder, -32767, 32767);
        }
    }
}
