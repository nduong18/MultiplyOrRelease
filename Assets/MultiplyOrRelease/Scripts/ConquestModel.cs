using System;
using System.Collections.Generic;
using UnityEngine;

namespace MultiplyOrRelease
{
    public sealed class ConquestCannon
    {
        public int owner;
        public Vector2 position;
        public long ammo, queued;
        public float angle, fireCredit;
        public int frameCooldown;
        public PlinkoBall[] balls;
    }

    // Fixed, local simulation clock: does not change Unity physics or Time.timeScale.
    public sealed partial class ConquestModel
    {
        public readonly ConquestConfig config;
        public readonly ConquestCannon[] cannons = new ConquestCannon[ConquestConfig.CountryCount];
        public readonly List<ShotState> shots = new List<ShotState>();
        public readonly int[] owners;
        public readonly Vector2[] pegs;
        public int winner { get; private set; } = -1;
        public bool Finished => winner >= 0;
        public float elapsed { get; private set; }
        public int boardVersion { get; private set; }
        public int captureCount { get; private set; }
        public long totalFired { get; private set; }
        public event Action<int, int, int> CannonCaptured;
        public event Action<int> ShotFired;
        public event Action<int> Won;
        readonly System.Random random;
        readonly Stack<ShotState> freeShots = new Stack<ShotState>();
        int nextShot, fireCursor;
        bool advancingShots;
        public int CountryCount
        {
            get { int count = 0; for (int t = 0; t < cannons.Length; t++) if (OwnsCannon(t)) count++; return count; }
        }
        public bool OwnsCannon(int owner)
        { for (int i = 0; i < cannons.Length; i++) if (cannons[i].owner == owner) return true; return false; }

        public ConquestModel(ConquestConfig source, int seed)
        {
            config = source; config.Validate(); random = new System.Random(seed);
            owners = new int[config.GridColumns * config.GridRows];
            for (int y = 0; y < config.GridRows; y++) for (int x = 0; x < config.GridColumns; x++)
                owners[y * config.GridColumns + x] = (3 - y / config.cellsPerCountry) * 6 + x / config.cellsPerCountry;
            for (int slot = 0; slot < cannons.Length; slot++)
            {
                var cannon = new ConquestCannon { owner = slot, ammo = config.cannon.initialAmmo,
                    position = new Vector2((-2.5f + slot % 6) * config.arenaSize.x / 6,
                        (1.5f - slot / 6) * config.arenaSize.y / 4),
                    angle = config.countries[slot].aimDegrees, balls = new PlinkoBall[config.plinko.ballCount] };
                cannons[slot] = cannon;
                for (int b = 0; b < cannon.balls.Length; b++)
                {
                    cannon.balls[b] = new PlinkoBall();
                    ResetBall(cannon.balls[b], (slot + b * cannons.Length) * config.plinko.initialStagger);
                }
            }
            pegs = new Vector2[config.plinko.rows * config.plinko.columns]; UpdatePegs();
            InitializePlinkoBoosts();
        }
        float Rand(float low, float high) => low + (float)random.NextDouble() * (high - low);
        void ResetBall(PlinkoBall ball, float delay)
        {
            float center = config.PlinkoLeft + config.plinko.width * .5f;
            float spread = Mathf.Min(config.plinko.spawnSpread, config.plinko.width * .5f - config.plinko.ballRadius * 2);
            ball.position = new Vector2(center + Rand(-spread, spread), config.arenaSize.y * .5f - config.plinko.spawnTopInset);
            ball.velocity = new Vector2(Rand(-config.plinko.horizontalKick, config.plinko.horizontalKick), 0);
            ball.delay = Mathf.Max(0, delay); ball.age = 0; ball.active = delay <= 0; ball.cycles++;
        }
        public void UpdatePegs()
        {
            var p = config.plinko;
            float bottom = -config.arenaSize.y * .5f + p.gateHeight + p.pegBottomInset;
            float top = config.arenaSize.y * .5f - p.pegTopInset;
            float height = Mathf.Max(.5f, top - bottom), spacing = height / p.rows;
            float span = p.width - p.wallInset * 2;
            // Full pegs must leave a marble-sized passage beside each wall. Alternate
            // rows intentionally have half pegs centred on the walls, as in the reference.
            float fullPegInset = Mathf.Min(span * .45f,
                Mathf.Max(span / (p.columns * 2), p.pegRadius + p.ballRadius * 2 + .02f));
            for (int row = 0; row < p.rows; row++) for (int col = 0; col < p.columns; col++)
            {
                float x = row % 2 == 0 ? Mathf.Lerp(fullPegInset, span - fullPegInset, col / (float)(p.columns - 1))
                    : span * col / (p.columns - 1);
                pegs[row * p.columns + col] = new Vector2(config.PlinkoLeft + p.wallInset + x,
                    bottom + Mathf.Repeat(row * spacing + elapsed * config.obstacleRiseSpeed, height));
            }
        }
        public long DisplayAmmo(int slot) => config.presentation.showReleaseCountdown && cannons[slot].queued > 0
            ? Math.Max(config.cannon.ammoAfterRelease, cannons[slot].queued) : cannons[slot].ammo;
        public void Multiply(int slot)
            => MultiplyAmmo(slot, config.cannon.multiplier);
        void MultiplyAmmo(int slot, int multiplier)
        {
            if (Finished || slot < 0 || slot >= cannons.Length) return;
            var s = cannons[slot]; long max = config.cannon.maxStoredAmmo;
            s.ammo = s.ammo > max / multiplier ? max : s.ammo * multiplier;
        }
        public void Release(int slot)
        {
            if (Finished || slot < 0 || slot >= cannons.Length) return;
            var s = cannons[slot];
            s.queued = s.queued > long.MaxValue - s.ammo ? long.MaxValue : s.queued + s.ammo;
            s.ammo = config.cannon.ammoAfterRelease;
        }
        public void CaptureCannon(int slot, int attacker)
        {
            if (Finished || slot < 0 || slot >= cannons.Length || attacker < 0 || attacker >= cannons.Length || !OwnsCannon(attacker)) return;
            var s = cannons[slot]; int previous = s.owner;
            if (previous == attacker) return;
            s.owner = attacker; s.ammo = config.ammoAfterCapture; s.queued = 0; s.fireCredit = 0; s.frameCooldown = 0;
            // The source bank is gone. Airborne shots cannot revive an eliminated country.
            if (!advancingShots && !OwnsCannon(previous)) for (int i = shots.Count - 1; i >= 0; i--)
                if (shots[i].team == previous) RemoveShot(i);
            captureCount++; CannonCaptured?.Invoke(slot, previous, attacker);
            int soleOwner = cannons[0].owner;
            for (int i = 1; i < cannons.Length; i++) if (cannons[i].owner != soleOwner) return;
            winner = soleOwner; Won?.Invoke(winner);
        }
        public void Tick(float dt)
        {
            if (Finished || dt <= 0) return;
            elapsed += dt; UpdatePegs();
            for (int i = 0; i < cannons.Length; i++)
            {
                var s = cannons[i]; var team = config.countries[s.owner];
                float sweep = elapsed * team.sweepSpeed + team.sweepPhase * team.sweepDegrees;
                float offset = team.sweepMode == SweepMode.Continuous ? sweep :
                    Mathf.PingPong(sweep, Mathf.Max(.001f, team.sweepDegrees)) - team.sweepDegrees * .5f;
                s.angle = team.aimDegrees + (team.clockwise ? -offset : offset);
                if (config.plinko.pauseWhileReleasing && s.queued > 0) continue;
                for (int b = 0; b < s.balls.Length; b++) StepBall(i, s.balls[b], dt);
            }
            // Pair separation changes positions too: solve all contacts together, then
            // enforce the walls again before gate checks or rendering.
            for (int pass = 0; pass < 6; pass++)
            {
                for (int slot = 0; slot < cannons.Length; slot++)
                    if (!IsPlinkoPaused(slot)) foreach (var ball in cannons[slot].balls)
                        if (ball.active) ResolvePlinkoContacts(ball);
                if (!config.plinko.collideBalls) break;
                CollideBalls();
            }
            for (int slot = 0; slot < cannons.Length; slot++)
                if (!IsPlinkoPaused(slot)) foreach (var ball in cannons[slot].balls)
                    if (ball.active)
                    {
                        ResolvePlinkoContacts(ball);
                        CollectPlinkoBoosts(slot, ball);
                        FinishBallStep(slot, ball);
                    }
            if (config.cannon.firingMode == FiringMode.ShotsPerSecond) FireTick(dt);
            StepShots(dt);
        }
        void StepBall(int slot, PlinkoBall ball, float dt)
        {
            if (!ball.active)
            {
                ball.delay -= dt; if (ball.delay > 0) return; ball.active = true;
            }
            var p = config.plinko;
            BeginPlinkoBoostStep(ball);
            ball.age += dt; ball.velocity.y -= p.gravity * dt;
            ball.position += ball.velocity * dt;
        }
        void ClampPlinkoWalls(PlinkoBall ball)
        {
            var p = config.plinko;
            float left = config.PlinkoLeft + p.wallInset + p.ballRadius;
            float right = config.PlinkoLeft + p.width - p.wallInset - p.ballRadius;
            if (ball.position.x < left)
            { ball.position.x = left; if (ball.velocity.x < 0) ball.velocity.x = -ball.velocity.x * p.restitution; }
            if (ball.position.x > right)
            { ball.position.x = right; if (ball.velocity.x > 0) ball.velocity.x = -ball.velocity.x * p.restitution; }
            float top = config.arenaSize.y * .5f - p.ballRadius;
            if (ball.position.y > top)
            { ball.position.y = top; if (ball.velocity.y > 0) ball.velocity.y = -ball.velocity.y * p.restitution; }
        }
        void ResolvePlinkoContacts(PlinkoBall ball)
        {
            var p = config.plinko;
            ClampPlinkoWalls(ball);
            float left = config.PlinkoLeft + p.wallInset + p.ballRadius;
            float right = config.PlinkoLeft + p.width - p.wallInset - p.ballRadius;
            float top = config.arenaSize.y * .5f - p.ballRadius;
            float contact = p.ballRadius + p.pegRadius;
            // A correction against one peg can overlap a neighbouring peg. Iterate
            // the fixed obstacles until settled, stopping early for free marbles.
            for (int iteration = 0; iteration < 64; iteration++)
            {
                bool corrected = false;
                foreach (var peg in pegs)
                {
                    Vector2 diff = ball.position - peg;
                    if (diff.sqrMagnitude >= (contact - .00001f) * (contact - .00001f)) continue;
                    corrected = true;
                    float distance = diff.magnitude;
                    var normal = distance > .0001f ? diff / distance : Vector2.down;
                    Vector2 target = peg + normal * contact;
                    // Radial projection alone can put a marble outside the wall. Project
                    // onto the peg's circle within the legal centre interval instead.
                    if (target.x < left || target.x > right)
                    {
                        target.x = Mathf.Clamp(target.x, left, right);
                        float dx = target.x - peg.x;
                        float dy = Mathf.Sqrt(Mathf.Max(0, contact * contact - dx * dx));
                        target.y = peg.y + (diff.y > 0 ? dy : -dy);
                        if (target.y > top) target.y = peg.y - dy;
                    }
                    if (target.y > top)
                    {
                        target.y = top;
                        float dy = target.y - peg.y;
                        float dx = Mathf.Sqrt(Mathf.Max(0, contact * contact - dy * dy));
                        float direction = diff.x != 0 ? Mathf.Sign(diff.x) :
                            peg.x < config.PlinkoLeft + p.width * .5f ? 1 : -1;
                        target.x = peg.x + direction * dx;
                        if (target.x < left || target.x > right)
                        { target.x = peg.x - direction * dx; }
                    }
                    ball.position = target;
                    normal = (target - peg).normalized;
                    var relative = ball.velocity - Vector2.up * config.obstacleRiseSpeed;
                    float speed = Vector2.Dot(relative, normal);
                    if (speed < 0) ball.velocity -= (1 + p.restitution) * speed * normal;
                    ball.velocity *= 1 - p.damping;
                }
                ClampPlinkoWalls(ball);
                if (!corrected) break;
            }
        }
        void FinishBallStep(int slot, PlinkoBall ball)
        {
            var p = config.plinko;
            if (ball.position.y <= -config.arenaSize.y * .5f + p.gateHeight + p.ballRadius)
            {
                if ((ball.position.x - config.PlinkoLeft) / p.width < p.multiplyRegion) Multiply(slot); else Release(slot);
                ResetBall(ball, p.recycleDelay);
            }
            else if (ball.age > p.maxFallTime) ResetBall(ball, p.recycleDelay);
        }
        void CollideBalls()
        {
            int count = config.plinko.ballCount * cannons.Length;
            float diameter = config.plinko.ballRadius * 2;
            for (int i = 0; i < count; i++)
            {
                var a = cannons[i / config.plinko.ballCount].balls[i % config.plinko.ballCount];
                if (!a.active || IsPlinkoPaused(i / config.plinko.ballCount)) continue;
                for (int j = i + 1; j < count; j++)
                {
                    var b = cannons[j / config.plinko.ballCount].balls[j % config.plinko.ballCount];
                    if (!b.active || IsPlinkoPaused(j / config.plinko.ballCount)) continue;
                    Vector2 diff = b.position - a.position;
                    if (diff.sqrMagnitude >= diameter * diameter) continue;
                    float distance = diff.magnitude;
                    var normal = distance > .0001f ? diff / distance : Vector2.right;
                    var correction = normal * ((diameter - distance) * .5f);
                    a.position -= correction; b.position += correction;
                    float relative = Vector2.Dot(b.velocity - a.velocity, normal);
                    if (relative < 0)
                    { var impulse = normal * (relative * .5f * (1 + config.plinko.restitution)); a.velocity += impulse; b.velocity -= impulse; }
                }
            }
        }
        bool IsPlinkoPaused(int slot) => config.plinko.pauseWhileReleasing && cannons[slot].queued > 0;
        void FireTick(float dt)
        {
            int budget = config.projectile.maxSpawnsPerTick;
            for (int n = 0; n < cannons.Length; n++)
            {
                int slot = (fireCursor + n) % cannons.Length; var s = cannons[slot];
                if (s.queued <= 0) { s.fireCredit = 0; continue; }
                s.fireCredit = Mathf.Min(2, s.fireCredit + dt * config.cannon.shotsPerSecond);
                while (s.fireCredit >= 1 && s.queued > 0 && budget > 0 && shots.Count < config.projectile.maxActive)
                { SpawnShot(slot); s.fireCredit--; budget--; }
            }
            fireCursor = (fireCursor + 1) % cannons.Length;
        }
        public void AdvanceFiringFrame()
        {
            if (Finished || config.cannon.firingMode != FiringMode.FramesBetweenShots) return;
            int budget = config.projectile.maxSpawnsPerTick;
            for (int n = 0; n < cannons.Length; n++)
            {
                int slot = (fireCursor + n) % cannons.Length; var s = cannons[slot];
                if (s.frameCooldown > 0) { s.frameCooldown--; continue; }
                if (s.queued > 0 && budget > 0 && shots.Count < config.projectile.maxActive)
                { SpawnShot(slot); s.frameCooldown = config.cannon.framesBetweenShots - 1; budget--; }
            }
            fireCursor = (fireCursor + 1) % cannons.Length;
        }
        void SpawnShot(int slot)
        {
            var s = cannons[slot]; float angle = (s.angle + Rand(-config.projectile.spreadDegrees, config.projectile.spreadDegrees)) * Mathf.Deg2Rad;
            var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            var shot = freeShots.Count > 0 ? freeShots.Pop() : new ShotState();
            shot.id = ++nextShot; shot.team = s.owner; shot.age = 0; shot.borderCollisions = 0;
            shot.position = s.position + direction * (config.cannon.hitRadius + config.projectile.radius + .02f);
            shot.velocity = direction * config.projectile.speed;
            shots.Add(shot); s.queued--; totalFired++; ShotFired?.Invoke(slot);
        }
        void RemoveShot(int index) { freeShots.Push(shots[index]); shots.RemoveAt(index); }
        // Distance to the entire swept segment prevents tunnelling through a cannon.
        public static float SegmentDistanceSquared(Vector2 a, Vector2 b, Vector2 point)
        {
            Vector2 d = b - a; float t = d.sqrMagnitude > .000001f ? Mathf.Clamp01(Vector2.Dot(point - a, d) / d.sqrMagnitude) : 0;
            return (point - (a + d * t)).sqrMagnitude;
        }
        void StepShots(float dt)
        {
            // Defer extinct-country cleanup while iterating: each projectile advances exactly once.
            advancingShots = true;
            int i = shots.Count - 1;
            while (i >= 0 && !Finished)
            {
                var shot = shots[i]; shot.age += dt;
                if (!OwnsCannon(shot.team) || shot.age > config.projectile.lifeTime) { RemoveShot(i--); continue; }
                Vector2 start = shot.position, end = start + shot.velocity * dt;
                bool removed = false;
                int steps = Mathf.Max(1, Mathf.CeilToInt((end - start).magnitude /
                    (Mathf.Min(config.arenaSize.x / config.GridColumns, config.arenaSize.y / config.GridRows) * .4f)));
                Vector2 previous = start;
                for (int step = 1; step <= steps; step++)
                {
                    Vector2 point = Vector2.Lerp(start, end, step / (float)steps);
                    int target = -1; float closest = float.MaxValue;
                    for (int c = 0; c < cannons.Length; c++)
                    {
                        if (cannons[c].owner == shot.team) continue;
                        float radius = config.cannon.hitRadius + config.projectile.radius;
                        if (SegmentDistanceSquared(previous, point, cannons[c].position) > radius * radius) continue;
                        float distance = (cannons[c].position - previous).sqrMagnitude;
                        if (distance < closest) { target = c; closest = distance; }
                    }
                    if (target >= 0)
                    {
                        int attacker = shot.team; RemoveShot(i); CaptureCannon(target, attacker);
                        removed = true; break;
                    }
                    if (CaptureTerritory(point, shot.team))
                    {
                        if (config.projectile.despawnOnCapture) { RemoveShot(i); removed = true; break; }
                        if (config.projectile.bounceOnCapture) { shot.velocity = -shot.velocity; end = point; break; }
                    }
                    previous = point;
                }
                if (removed) { i--; continue; }
                shot.position = end;
                var half = config.arenaSize * .5f - Vector2.one * config.projectile.radius;
                bool edge = false;
                if (Mathf.Abs(end.x) > half.x) { edge = true; shot.position.x = Mathf.Clamp(end.x, -half.x, half.x); shot.velocity.x = -shot.velocity.x; }
                if (Mathf.Abs(end.y) > half.y) { edge = true; shot.position.y = Mathf.Clamp(end.y, -half.y, half.y); shot.velocity.y = -shot.velocity.y; }
                if (edge) shot.borderCollisions++;
                if (edge && (!config.projectile.bounceAtArenaEdge || config.projectile.maxBorderCollisions > 0 && shot.borderCollisions > config.projectile.maxBorderCollisions)) RemoveShot(i);
                i--;
            }
            advancingShots = false;
            for (int j = shots.Count - 1; j >= 0; j--) if (!OwnsCannon(shots[j].team)) RemoveShot(j);
        }
        public bool CaptureTerritory(Vector2 point, int owner)
        {
            if (point.x < -config.arenaSize.x / 2 || point.x >= config.arenaSize.x / 2 ||
                point.y < -config.arenaSize.y / 2 || point.y >= config.arenaSize.y / 2) return false;
            int x = Mathf.FloorToInt((point.x / config.arenaSize.x + .5f) * config.GridColumns);
            int y = Mathf.FloorToInt((point.y / config.arenaSize.y + .5f) * config.GridRows);
            if (owners[y * config.GridColumns + x] == owner) return false;
            int radius = config.projectile.captureRadiusCells;
            for (int dy = -radius; dy <= radius; dy++) for (int dx = -radius; dx <= radius; dx++)
            {
                int cx = x + dx, cy = y + dy;
                if (cx >= 0 && cy >= 0 && cx < config.GridColumns && cy < config.GridRows) owners[cy * config.GridColumns + cx] = owner;
            }
            boardVersion++; return true;
        }
    }
}
