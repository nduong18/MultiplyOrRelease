using System;
using System.Collections.Generic;
using UnityEngine;

namespace MultiplyOrRelease
{
    public sealed partial class ConquestModel
    {
        sealed class BoostContact
        {
            public Vector2 from;
        }
        readonly Dictionary<PlinkoBall, BoostContact> boostContacts = new Dictionary<PlinkoBall, BoostContact>();
        float[] boostReadyAt;
        public event Action<int, int> PlinkoBoostCollected; // boost index, cannon slot
        public int plinkoBoostCollectionCount { get; private set; }

        void InitializePlinkoBoosts()
        {
            boostReadyAt = new float[config.plinkoBoosts.Length];
            foreach (var cannon in cannons) foreach (var ball in cannon.balls)
                boostContacts.Add(ball, new BoostContact());
        }
        public bool IsPlinkoBoostAvailable(int index)
            => config.enablePlinkoBoosts && config.plinkoBoosts[index] != null && config.plinkoBoosts[index].enabled
                && elapsed >= boostReadyAt[index];
        public Vector2 PlinkoBoostPosition(int index)
        {
            var boost = config.plinkoBoosts[index]; var p = config.plinko;
            float inset = Mathf.Max(p.wallInset + p.ballRadius, boost.radius + p.wallInset);
            float bottom = -config.arenaSize.y * .5f + p.gateHeight + boost.radius;
            float top = config.arenaSize.y * .5f - Mathf.Max(boost.radius, p.ballRadius);
            return new Vector2(Mathf.Lerp(config.PlinkoLeft + inset, config.PlinkoLeft + p.width - inset, boost.position.x),
                Mathf.Lerp(bottom, top, boost.position.y));
        }
        void BeginPlinkoBoostStep(PlinkoBall ball)
        {
            var state = boostContacts[ball]; state.from = ball.position;
        }
        void CollectPlinkoBoosts(int slot, PlinkoBall ball)
        {
            if (!config.enablePlinkoBoosts) return;
            var state = boostContacts[ball];
            for (int i = 0; i < config.plinkoBoosts.Length; i++)
            {
                var boost = config.plinkoBoosts[i];
                if (!IsPlinkoBoostAvailable(i)) continue;
                var center = PlinkoBoostPosition(i);
                float radius = boost.radius + config.plinko.ballRadius;
                // Swept detection handles fast marbles; recycling never sweeps from the gate to the top.
                bool hit = SegmentDistanceSquared(state.from, ball.position, center) <= radius * radius;
                if (hit)
                {
                    // Reserve it before applying the reward: all marbles share this timer.
                    boostReadyAt[i] = elapsed + boost.respawnDelay;
                    if (boost.kind == PlinkoBoostKind.MultiplyAmmo) MultiplyAmmo(slot, boost.ammoMultiplier);
                    else
                    {
                        ball.velocity *= boost.speedMultiplier;
                        ball.velocity.y = Mathf.Min(ball.velocity.y, -boost.minimumFallSpeed);
                        ball.velocity = Vector2.ClampMagnitude(ball.velocity, boost.maxSpeed);
                    }
                    plinkoBoostCollectionCount++; PlinkoBoostCollected?.Invoke(i, slot);
                }
            }
        }
    }
}
