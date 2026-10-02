using System;
using System.Collections.Generic;
using UnityEngine;

namespace MultiplyOrRelease
{
    public sealed class BoostState
    {
        public int id;
        public BoostKind kind;
        public Vector2 position;
        public float expiresAt;
        public BoostCollection flight;
    }

    public sealed class BoostCollection
    {
        public BoostState pickup;
        public int team;
        public Vector2 target;
        public float startedAt, duration;
        public bool completed, canceled;
    }

    public sealed partial class SimulationModel
    {
        public readonly List<BoostState> boosts = new List<BoostState>();
        public readonly List<BoostCollection> collectingBoosts = new List<BoostCollection>();
        public event Action<BoostState, int> BoostCollected;
        System.Random boostRandom;
        float nextBoostSpawn;
        int nextBoostId;

        void InitializeBoosts(int seed)
        {
            // Spawning pickups does not consume the Plinko/projectile random stream.
            boostRandom = new System.Random(unchecked(seed ^ 0x5b007123));
            nextBoostSpawn = config.boosts.firstSpawnDelay;
        }

        public float FireRateScale(int team)
            => teams[team].alive && teams[team].fireRateBoostUntil > elapsed ? config.boosts.fireRateMultiplier : 1;

        void StepBoosts()
        {
            StepBoostCollections();
            for (int t = 0; t < 4; t++)
            {
                var team = teams[t];
                if (team.fireRateBoostUntil <= elapsed) team.fireRateBoostUntil = 0;
                for (int b = team.balls.Length - 1; b >= 0; b--)
                {
                    var ball = team.balls[b];
                    if (ball.expiresAt <= 0 || (team.alive && ball.expiresAt > elapsed)) continue;
                    ball.active = false;
                    // Permanent balls and the remaining temporary balls keep their state.
                    Array.Copy(team.balls, b + 1, team.balls, b, team.balls.Length - b - 1);
                    Array.Resize(ref team.balls, team.balls.Length - 1);
                }
            }
            for (int i = boosts.Count - 1; i >= 0; i--)
                if (boosts[i].expiresAt <= elapsed || !config.boosts.Appearance(boosts[i].kind).enabled) boosts.RemoveAt(i);
            var s = config.boosts;
            if (!s.enabled) { boosts.Clear(); return; }
            if (elapsed < nextBoostSpawn) return;
            nextBoostSpawn = elapsed + s.spawnIntervalMin + (float)boostRandom.NextDouble() * (s.spawnIntervalMax - s.spawnIntervalMin);
            if (boosts.Count >= s.maxActive) return;
            float fireRateWeight = s.fireRate.EffectiveSpawnWeight;
            float doubleAmmoWeight = s.doubleAmmo.EffectiveSpawnWeight;
            float weight = fireRateWeight + doubleAmmoWeight + s.extraMarble.EffectiveSpawnWeight;
            if (weight <= 0) return;
            float choice = (float)boostRandom.NextDouble() * weight;
            var kind = choice < fireRateWeight ? BoostKind.FireRate
                : choice < fireRateWeight + doubleAmmoWeight ? BoostKind.DoubleAmmo : BoostKind.ExtraMarble;
            float extent = Mathf.Max(0, config.board.size * .5f - s.radius - s.edgeInset);
            for (int attempt = 0; attempt < 40; attempt++)
            {
                var position = new Vector2(((float)boostRandom.NextDouble() * 2 - 1) * extent,
                    ((float)boostRandom.NextDouble() * 2 - 1) * extent);
                if (SpawnBoost(kind, position) != null) return;
            }
        }

        // Also useful for controlled simulation previews; normal gameplay uses StepBoosts.
        public BoostState SpawnBoost(BoostKind kind, Vector2 position)
        {
            var s = config.boosts;
            if (!s.enabled || phase != MatchPhase.Running || boosts.Count >= s.maxActive ||
                (int)kind < 0 || (int)kind > 2 || !float.IsFinite(position.x) || !float.IsFinite(position.y)) return null;
            if (!s.Appearance(kind).enabled) return null;
            float extent = config.board.size * .5f - s.radius - s.edgeInset;
            if (Mathf.Abs(position.x) > extent || Mathf.Abs(position.y) > extent) return null;
            float clearance = s.radius + config.cannon.hitRadius + s.cannonClearance;
            foreach (var team in teams)
                if ((position - team.cannonPosition).sqrMagnitude < clearance * clearance) return null;
            foreach (var boost in boosts)
                if ((position - boost.position).sqrMagnitude < 4 * s.radius * s.radius) return null;
            var item = new BoostState { id = ++nextBoostId, kind = kind, position = position, expiresAt = elapsed + s.pickupLifetime };
            boosts.Add(item); return item;
        }

        bool TryHitBoost(ShotState shot, Vector2 from, Vector2 to)
        {
            if (!config.boosts.enabled || phase != MatchPhase.Running || !teams[shot.team].alive) return false;
            Vector2 segment = to - from;
            float radius = config.boosts.radius + config.projectile.radius;
            int nearest = -1;
            float nearestEntry = float.PositiveInfinity;
            for (int i = 0; i < boosts.Count; i++)
            {
                // Find the first circle along the segment, including an initial overlap.
                Vector2 offset = from - boosts[i].position;
                float entry = 0;
                float c = offset.sqrMagnitude - radius * radius;
                if (c > 0)
                {
                    float a = segment.sqrMagnitude;
                    if (a <= .0000001f) continue;
                    float b = Vector2.Dot(offset, segment);
                    float discriminant = b * b - a * c;
                    if (discriminant < 0) continue;
                    entry = (-b - Mathf.Sqrt(discriminant)) / a;
                    if (entry < 0 || entry > 1) continue;
                }
                if (entry < nearestEntry) { nearest = i; nearestEntry = entry; }
            }
            if (nearest < 0) return false;
            var pickup = boosts[nearest];
            boosts.RemoveAt(nearest); // A second bullet can never claim the same pickup.
            if (config.boosts.animateCollection)
            {
                pickup.flight = new BoostCollection
                {
                    pickup = pickup, team = shot.team, target = teams[shot.team].cannonPosition,
                    startedAt = elapsed, duration = config.boosts.collectionFlightDuration
                };
                collectingBoosts.Add(pickup.flight);
            }
            else ApplyBoost(shot.team, pickup.kind);
            BoostCollected?.Invoke(pickup, shot.team);
            return true;
        }

        static long DoubleClamped(long amount, long ceiling)
            => amount > ceiling / 2 ? ceiling : amount * 2;

        void StepBoostCollections()
        {
            if (AliveCount <= 1 || (config.matchTimeLimit > 0 && elapsed >= config.matchTimeLimit))
            {
                CancelBoostCollections();
                return;
            }
            for (int i = 0; i < collectingBoosts.Count;)
            {
                var flight = collectingBoosts[i];
                if (elapsed - flight.startedAt < flight.duration) { i++; continue; }
                flight.completed = true;
                collectingBoosts.RemoveAt(i);
                // A destroyed cannon cannot receive a reward or be revived by a delivery.
                if (teams[flight.team].alive) ApplyBoost(flight.team, flight.pickup.kind);
            }
        }

        void CancelBoostCollections()
        {
            foreach (var flight in collectingBoosts) flight.canceled = true;
            collectingBoosts.Clear();
        }

        void ApplyBoost(int t, BoostKind kind)
        {
            var team = teams[t];
            var s = config.boosts;
            switch (kind)
            {
                case BoostKind.FireRate:
                    team.fireRateBoostUntil = elapsed + s.fireRateDuration;
                    team.lastEvent = "BOOST SPEED";
                    break;
                case BoostKind.DoubleAmmo:
                    team.ammo = DoubleClamped(team.ammo, config.cannon.maxStoredAmmo);
                    team.queued = DoubleClamped(team.queued, long.MaxValue);
                    team.lastEvent = "BOOST x2 AMMO";
                    break;
                case BoostKind.ExtraMarble:
                    int count = 0;
                    foreach (var existing in team.balls) if (existing.expiresAt > 0) count++;
                    if (count < s.maxExtraMarbles)
                    {
                        var ball = new PlinkoBall { expiresAt = elapsed + s.extraMarbleDuration };
                        ResetBall(ball, 0);
                        Array.Resize(ref team.balls, team.balls.Length + 1);
                        team.balls[team.balls.Length - 1] = ball;
                    }
                    else
                    {
                        // At the cap, refresh the oldest extra marble rather than losing the pickup.
                        PlinkoBall oldest = null;
                        foreach (var ball in team.balls)
                            if (ball.expiresAt > 0 && (oldest == null || ball.expiresAt < oldest.expiresAt)) oldest = ball;
                        if (oldest != null) oldest.expiresAt = elapsed + s.extraMarbleDuration;
                    }
                    team.lastEvent = "BOOST +1 MARBLE";
                    break;
            }
            team.eventTime = elapsed;
        }
    }
}
