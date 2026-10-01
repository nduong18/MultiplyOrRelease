using System;
using System.Collections.Generic;
using UnityEngine;

namespace MultiplyOrRelease
{
    public enum MatchPhase { Ready, Running, Settling, Finished }
    public sealed class TeamState
    {
        public bool alive = true;
        public int health;
        public long ammo, queued, fired;
        public int multiplies, releases, captures;
        public float angle, fireCredit;
        public Vector2 cannonPosition;
        public PlinkoBall[] balls;
        public Vector2[] pegs;
        public string lastEvent = "READY";
        public float eventTime;
    }
    public sealed class PlinkoBall
    {
        public Vector2 position, velocity;
        public float delay, age;
        public int cycles;
        public bool active;
    }
    public sealed class ShotState
    {
        public int id, team;
        public Vector2 position, velocity;
        public float age;
    }

    // All randomness and motion use a local fixed clock: pausing/speed changes never
    // alter Unity's global time scale or physics settings. Rendering is independent.
    public sealed class SimulationModel
    {
        public readonly SimulationConfig config;
        public readonly TeamState[] teams = new TeamState[4];
        public readonly List<ShotState> shots = new List<ShotState>();
        public readonly int[] owners;
        public readonly int[] territoryCounts = new int[4];
        public MatchPhase phase { get; private set; } = MatchPhase.Ready;
        public float elapsed { get; private set; }
        public int winner { get; private set; } = -1;
        public int boardVersion { get; private set; }
        public string resultReason { get; private set; }
        public long totalFired { get; private set; }
        readonly System.Random random;
        readonly Stack<ShotState> shotPool = new Stack<ShotState>();
        int nextShotId, fireCursor;
        float finishTimer;
        public float CellWidth => config.board.size / config.board.columns;
        public float CellHeight => config.board.size / config.board.rows;
        public int AliveCount
        {
            get { int n = 0; for (int i = 0; i < 4; i++) if (teams[i].alive) n++; return n; }
        }

        public SimulationModel(SimulationConfig source, int seed)
        {
            config = source;
            config.Validate();
            random = new System.Random(seed);
            owners = new int[config.board.columns * config.board.rows];
            for (int y = 0; y < config.board.rows; y++)
                for (int x = 0; x < config.board.columns; x++)
                {
                    int quadrant = (y >= config.board.rows / 2 ? 0 : 2) + (x >= config.board.columns / 2 ? 1 : 0);
                    int owner = config.board.quadrantOwners != null && config.board.quadrantOwners.Length == 4
                        ? Mathf.Clamp(config.board.quadrantOwners[quadrant], 0, 3) : quadrant;
                    owners[y * config.board.columns + x] = owner;
                    territoryCounts[owner]++;
                }
            float corner = config.board.size / 2 - config.cannon.cornerInset;
            for (int t = 0; t < 4; t++)
            {
                var team = new TeamState
                {
                    health = config.teams[t].hitPoints,
                    ammo = config.cannon.initialAmmo,
                    angle = config.teams[t].aimDegrees,
                    cannonPosition = new Vector2(t % 2 == 0 ? -corner : corner, t < 2 ? corner : -corner),
                    balls = new PlinkoBall[config.plinko.ballCount],
                    pegs = MakePegs()
                };
                teams[t] = team;
                for (int b = 0; b < team.balls.Length; b++)
                {
                    team.balls[b] = new PlinkoBall();
                    ResetBall(team.balls[b], b * config.plinko.initialStagger + (float)random.NextDouble() * .3f);
                }
            }
        }

        public void Start() { if (phase == MatchPhase.Ready) phase = MatchPhase.Running; }
        float Range(float min, float max) { return min + (float)random.NextDouble() * (max - min); }
        Vector2[] MakePegs()
        {
            var p = config.plinko;
            var result = new List<Vector2>();
            float spacing = (p.width - .5f) / p.columns;
            for (int y = 0; y < p.rows; y++)
                for (int x = 0; x <= p.columns; x++)
                {
                    float px = (x - p.columns * .5f + (y % 2 == 0 ? 0 : .5f)) * spacing;
                    if (Mathf.Abs(px) < p.width * .5f - .14f)
                        result.Add(new Vector2(px, Mathf.Lerp(p.height * .5f - p.pegTopInset, -p.height * .5f + p.pegBottomInset, y / (float)(p.rows - 1))));
                }
            return result.ToArray();
        }
        void ResetBall(PlinkoBall ball, float delay)
        {
            var p = config.plinko;
            float width = p.width * .5f - p.ballRadius - p.wallInset;
            ball.position = new Vector2(Range(-width, width) * Mathf.Clamp01(p.spawnSpread), p.height * .5f - p.spawnTopInset);
            ball.velocity = new Vector2(Range(-p.horizontalKick, p.horizontalKick), -.1f);
            ball.delay = delay;
            ball.age = 0;
            ball.active = delay <= 0;
        }

        public void Tick(float dt)
        {
            if (phase == MatchPhase.Ready || phase == MatchPhase.Finished) return;
            elapsed += dt;
            if (phase == MatchPhase.Running)
            {
                for (int t = 0; t < 4; t++) if (teams[t].alive)
                {
                    UpdateCannon(t);
                    StepPlinko(t, dt);
                }
                FireQueued(dt);
            }
            StepShots(dt);
            if (phase == MatchPhase.Running && AliveCount <= 1)
            {
                phase = MatchPhase.Settling;
                for (int i = 0; i < 4; i++) teams[i].queued = 0;
            }
            if (phase == MatchPhase.Settling && shots.Count == 0)
            {
                finishTimer += dt;
                if (finishTimer >= config.resultDelay)
                {
                    winner = -1;
                    for (int t = 0; t < 4; t++) if (teams[t].alive) winner = t;
                    resultReason = winner < 0 ? "All cannons destroyed" : "Last cannon standing";
                    phase = MatchPhase.Finished;
                }
            }
            if (phase == MatchPhase.Running && config.matchTimeLimit > 0 && elapsed >= config.matchTimeLimit)
            {
                int best = -1;
                bool tie = false;
                for (int t = 0; t < 4; t++) if (teams[t].alive)
                {
                    if (best < 0 || territoryCounts[t] > territoryCounts[best]) { best = t; tie = false; }
                    else if (territoryCounts[t] == territoryCounts[best]) tie = true;
                }
                winner = tie ? -1 : best;
                resultReason = "Time limit — territory ranking";
                for (int t = 0; t < 4; t++) teams[t].queued = 0;
                phase = MatchPhase.Finished;
            }
        }
        void UpdateCannon(int t)
        {
            var s = config.teams[t];
            float direction = s.clockwise ? -1 : 1;
            if (s.sweepDegrees <= 0) { teams[t].angle = s.aimDegrees; return; }
            float travel = elapsed * s.sweepSpeed + s.sweepPhase * s.sweepDegrees;
            float offset = s.sweepMode == SweepMode.PingPong
                ? Mathf.PingPong(travel, s.sweepDegrees) - s.sweepDegrees * .5f
                : Mathf.Repeat(travel, s.sweepDegrees) - s.sweepDegrees * .5f;
            teams[t].angle = s.aimDegrees + direction * offset;
        }
        void StepPlinko(int t, float dt)
        {
            var team = teams[t];
            var p = config.plinko;
            float left = -p.width * .5f + p.ballRadius + p.wallInset;
            float right = -left;
            float hitDistance = p.ballRadius + p.pegRadius;
            foreach (var ball in team.balls)
            {
                if (!ball.active)
                {
                    ball.delay -= dt;
                    if (ball.delay <= 0) ball.active = true;
                    else continue;
                }
                ball.age += dt;
                ball.velocity.y -= p.gravity * dt;
                ball.velocity *= Mathf.Max(0, 1 - p.damping * dt);
                ball.position += ball.velocity * dt;
                if (ball.position.x < left) { ball.position.x = left; ball.velocity.x = Mathf.Abs(ball.velocity.x) * p.restitution; }
                if (ball.position.x > right) { ball.position.x = right; ball.velocity.x = -Mathf.Abs(ball.velocity.x) * p.restitution; }
                // Two passes keep contacts stable when a ball sits between pegs.
                for (int pass = 0; pass < 2; pass++) foreach (var peg in team.pegs)
                {
                    Vector2 d = ball.position - peg;
                    float sq = d.sqrMagnitude;
                    if (sq >= hitDistance * hitDistance) continue;
                    Vector2 normal = sq > .000001f ? d / Mathf.Sqrt(sq) : Vector2.up;
                    ball.position = peg + normal * hitDistance;
                    float into = Vector2.Dot(ball.velocity, normal);
                    if (into < 0) ball.velocity -= (1 + p.restitution) * into * normal;
                    if (normal.y > .7f && Mathf.Abs(ball.velocity.x) < .15f)
                        ball.velocity.x += Range(-.3f, .3f);
                }
                float gateY = -p.height * .5f + p.gateHeight + p.ballRadius;
                if (ball.position.y < gateY)
                {
                    float u = (ball.position.x + p.width * .5f) / p.width;
                    bool multiply = p.mirrorRightBoards && t % 2 == 1 ? u > 1 - p.multiplyRegion : u < p.multiplyRegion;
                    if (multiply) Multiply(t); else Release(t);
                    ball.cycles++;
                    ResetBall(ball, p.recycleDelay);
                }
                else if (ball.age > p.maxFallTime)
                {
                    // Recover a physically stuck ball without awarding a gate event.
                    ResetBall(ball, p.recycleDelay);
                }
            }
            if (p.collideBalls)
                for (int a = 0; a < team.balls.Length; a++)
                    for (int b = a + 1; b < team.balls.Length; b++)
                    {
                        var ba = team.balls[a]; var bb = team.balls[b];
                        if (!ba.active || !bb.active) continue;
                        Vector2 delta = ba.position - bb.position;
                        float distance = delta.magnitude, diameter = 2 * p.ballRadius;
                        if (distance >= diameter) continue;
                        Vector2 normal = distance > .0001f ? delta / distance : Vector2.right;
                        Vector2 correction = normal * (diameter - distance) * .5f;
                        ba.position += correction; bb.position -= correction;
                        float into = Vector2.Dot(ba.velocity - bb.velocity, normal);
                        if (into < 0)
                        {
                            Vector2 impulse = normal * into * (1 + p.restitution) * .5f;
                            ba.velocity -= impulse; bb.velocity += impulse;
                        }
                    }
        }
        public void Multiply(int t)
        {
            if (phase != MatchPhase.Running || !teams[t].alive) return;
            var s = teams[t];
            long ceiling = config.cannon.maxStoredAmmo;
            s.ammo = s.ammo > ceiling / config.cannon.multiplier ? ceiling : s.ammo * config.cannon.multiplier;
            s.multiplies++;
            s.lastEvent = s.ammo == ceiling ? "×" + config.cannon.multiplier + "  ·  MAX" : "×" + config.cannon.multiplier;
            s.eventTime = elapsed;
        }
        public void Release(int t)
        {
            if (phase != MatchPhase.Running || !teams[t].alive) return;
            var s = teams[t];
            if (s.queued > long.MaxValue - s.ammo)
            {
                // Retain stored ammo when the queue cannot represent a complete volley.
                s.lastEvent = "QUEUE FULL"; s.eventTime = elapsed; return;
            }
            s.queued += s.ammo;
            s.lastEvent = "RELEASE " + s.ammo.ToString("N0");
            s.ammo = config.cannon.ammoAfterRelease;
            s.releases++;
            s.eventTime = elapsed;
        }
        void FireQueued(float dt)
        {
            int budget = config.projectile.maxSpawnsPerTick;
            for (int i = 0; i < 4; i++)
            {
                var s = teams[i];
                s.fireCredit = s.queued > 0 ? Mathf.Min(s.fireCredit + config.cannon.shotsPerSecond * dt, budget) : 0;
            }
            // Round robin makes the active-shot budget fair across all four teams.
            while (budget > 0 && shots.Count < config.projectile.maxActive)
            {
                bool fired = false;
                for (int i = 0; i < 4; i++)
                {
                    int t = fireCursor++ % 4;
                    var s = teams[t];
                    if (!s.alive || s.queued == 0 || s.fireCredit < 1) continue;
                    float radians = (s.angle + Range(-config.projectile.spreadDegrees, config.projectile.spreadDegrees)) * Mathf.Deg2Rad;
                    var direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
                    var shot = shotPool.Count > 0 ? shotPool.Pop() : new ShotState();
                    shot.id = ++nextShotId; shot.team = t; shot.age = 0;
                    shot.position = s.cannonPosition + direction * config.cannon.muzzleLength;
                    shot.velocity = direction * config.projectile.speed;
                    shots.Add(shot);
                    s.queued--; s.fireCredit--; s.fired++; totalFired++;
                    budget--; fired = true;
                    if (budget == 0 || shots.Count >= config.projectile.maxActive) break;
                }
                if (!fired) break;
            }
        }
        void RemoveShot(int index)
        {
            shotPool.Push(shots[index]);
            int last = shots.Count - 1;
            shots[index] = shots[last]; shots.RemoveAt(last);
        }
        void StepShots(float dt)
        {
            float half = config.board.size * .5f;
            float minCell = Mathf.Min(CellWidth, CellHeight);
            for (int i = shots.Count - 1; i >= 0; i--)
            {
                var shot = shots[i];
                shot.age += dt;
                bool remove = shot.age >= config.projectile.lifeTime;
                int steps = Mathf.Max(1, Mathf.CeilToInt(shot.velocity.magnitude * dt / (minCell * .3f)));
                for (int step = 0; step < steps && !remove; step++)
                {
                    Vector2 old = shot.position;
                    shot.position += shot.velocity * (dt / steps);
                    for (int t = 0; t < 4; t++)
                    {
                        if (t == shot.team || !teams[t].alive) continue;
                        Vector2 segment = shot.position - old;
                        float u = segment.sqrMagnitude > 0 ? Mathf.Clamp01(Vector2.Dot(teams[t].cannonPosition - old, segment) / segment.sqrMagnitude) : 0;
                        float radius = config.cannon.hitRadius + config.projectile.radius;
                        if ((old + segment * u - teams[t].cannonPosition).sqrMagnitude <= radius * radius)
                        {
                            teams[t].health--;
                            if (config.cannon.destroyOnEnemyHit || teams[t].health <= 0) Eliminate(t);
                            remove = true; break;
                        }
                    }
                    if (remove) break;
                    float limit = half - config.projectile.radius;
                    if (Mathf.Abs(shot.position.x) > limit)
                    {
                        if (!config.projectile.bounceAtArenaEdge) { remove = true; break; }
                        shot.position.x = Mathf.Clamp(shot.position.x, -limit, limit); shot.velocity.x *= -1;
                    }
                    if (Mathf.Abs(shot.position.y) > limit)
                    {
                        if (!config.projectile.bounceAtArenaEdge) { remove = true; break; }
                        shot.position.y = Mathf.Clamp(shot.position.y, -limit, limit); shot.velocity.y *= -1;
                    }
                    int x = Mathf.Clamp((int)((shot.position.x + half) / CellWidth), 0, config.board.columns - 1);
                    int y = Mathf.Clamp((int)((shot.position.y + half) / CellHeight), 0, config.board.rows - 1);
                    if (owners[y * config.board.columns + x] != shot.team)
                    {
                        Capture(x, y, shot.team);
                        if (config.projectile.despawnOnCapture)
                        {
                            remove = true;
                            break;
                        }
                        if (config.projectile.bounceOnCapture)
                        {
                            int ox = Mathf.FloorToInt((old.x + half) / CellWidth);
                            int oy = Mathf.FloorToInt((old.y + half) / CellHeight);
                            if (ox != x) shot.velocity.x *= -1;
                            if (oy != y) shot.velocity.y *= -1;
                            if (ox == x && oy == y) shot.velocity *= -1;
                            shot.position = old;
                        }
                    }
                }
                if (remove) RemoveShot(i);
            }
        }
        public void Capture(int x, int y, int team)
        {
            if (team < 0 || team >= 4) return;
            int r = config.projectile.captureRadiusCells;
            for (int dy = -r; dy <= r; dy++) for (int dx = -r; dx <= r; dx++)
            {
                int px = x + dx, py = y + dy;
                if (px < 0 || py < 0 || px >= config.board.columns || py >= config.board.rows) continue;
                int index = py * config.board.columns + px, old = owners[index];
                if (old == team) continue;
                owners[index] = team;
                territoryCounts[old]--; territoryCounts[team]++;
                teams[team].captures++; boardVersion++;
            }
        }
        public void Eliminate(int t)
        {
            if (!teams[t].alive) return;
            teams[t].alive = false; teams[t].health = 0; teams[t].queued = 0;
            teams[t].lastEvent = "ELIMINATED"; teams[t].eventTime = elapsed;
            foreach (var b in teams[t].balls) b.active = false;
        }
    }
}
