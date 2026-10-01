using System;
using MultiplyOrRelease;
using NUnit.Framework;
using UnityEngine;

public class SimulationModelTests
{
    SimulationConfig config;
    [SetUp] public void SetUp()
    {
        config = ScriptableObject.CreateInstance<SimulationConfig>(); config.Validate();
        config.teams[0].aimDegrees = -45; config.teams[1].aimDegrees = -135;
        config.teams[2].aimDegrees = 45; config.teams[3].aimDegrees = 135;
    }
    [TearDown] public void TearDown() { UnityEngine.Object.DestroyImmediate(config); }
    SimulationModel NewModel() { var m = new SimulationModel(config, 1234); m.Start(); return m; }
    [Test] public void FourQuadrantsAndFiveMarblesStartWithOneAmmo()
    {
        var m = NewModel();
        for (int i = 0; i < 4; i++)
        {
            Assert.AreEqual(1, m.teams[i].ammo); Assert.AreEqual(5, m.teams[i].balls.Length);
            Assert.AreEqual(m.owners.Length / 4, m.territoryCounts[i]);
        }
    }
    [Test] public void MultiplyThenReleaseQueuesWholeVolleyAndResetsStock()
    {
        var m = NewModel(); for (int i = 0; i < 5; i++) m.Multiply(0);
        Assert.AreEqual(32, m.teams[0].ammo); m.Release(0);
        Assert.AreEqual(32, m.teams[0].queued); Assert.AreEqual(1, m.teams[0].ammo);
    }
    [TestCase(FiringMode.ShotsPerSecond)] [TestCase(FiringMode.FramesBetweenShots)]
    public void ReleaseFreezesOnlyItsPlinkoUntilTheLastQueuedShot(FiringMode mode)
    {
        config.cannon.firingMode = mode; config.cannon.shotsPerSecond = 10;
        config.cannon.destroyOnEnemyHit = false;
        var m = NewModel();
        foreach (var team in m.teams) foreach (var b in team.balls) b.delay = 999;
        var ball = m.teams[0].balls[0]; ball.active = true;
        ball.position = new Vector2(0, 1); ball.velocity = Vector2.down;
        ball.age = .25f;
        Vector2 position = ball.position, velocity = ball.velocity;
        float delay = m.teams[0].balls[1].delay;
        m.teams[0].ammo = 4; m.Release(0);
        Assert.AreEqual(4, m.DisplayAmmo(0));
        int steps = 0;
        while (m.teams[0].queued > 0 && steps++ < 100)
        {
            long before = m.teams[0].queued;
            m.Tick(.01f); m.AdvanceFiringFrame();
            Assert.AreEqual(position, ball.position); Assert.AreEqual(velocity, ball.velocity);
            Assert.AreEqual(.25f, ball.age); Assert.AreEqual(0, ball.cycles);
            Assert.IsTrue(ball.active); Assert.AreEqual(delay, m.teams[0].balls[1].delay);
            Assert.LessOrEqual(m.teams[0].queued, before);
            Assert.AreEqual(Math.Max(1, m.teams[0].queued), m.DisplayAmmo(0));
        }
        Assert.AreEqual(0, m.teams[0].queued); Assert.AreEqual(4, m.teams[0].fired);
        Assert.AreEqual(1, m.DisplayAmmo(0)); Assert.IsFalse(m.IsPlinkoPaused(0));
        Assert.Less(m.teams[1].balls[0].delay, 999, "Other teams must keep advancing.");
        Assert.Greater(m.shots.Count, 0, "Resume must not wait for airborne shots.");
        m.Tick(.01f); Assert.AreNotEqual(position, ball.position);
        Assert.Greater(ball.age, .25f); Assert.Less(m.teams[0].balls[1].delay, delay);
    }
    [Test] public void FirstReleaseGateImmediatelyStopsLaterBallsInTheSameTick()
    {
        config.cannon.firingMode = FiringMode.FramesBetweenShots;
        var m = NewModel();
        foreach (var team in m.teams) foreach (var b in team.balls) b.delay = 999;
        m.teams[0].ammo = 8;
        var first = m.teams[0].balls[0]; var second = m.teams[0].balls[1];
        Vector2 gate = new Vector2(config.plinko.width * .25f, -config.plinko.height * .5f + .3f);
        first.active = second.active = true;
        first.position = second.position = gate; first.velocity = second.velocity = Vector2.down;
        m.Tick(1f / 120);
        Assert.AreEqual(8, m.teams[0].queued); Assert.AreEqual(1, m.teams[0].releases);
        Assert.AreEqual(1, first.cycles); Assert.AreEqual(0, second.cycles);
        Assert.AreEqual(gate, second.position); Assert.AreEqual(Vector2.down, second.velocity);
        Assert.AreEqual(0, second.age); Assert.AreEqual(8, m.DisplayAmmo(0));
    }
    [Test] public void ReleasePauseAndCountdownCanBeDisabledIndependently()
    {
        config.plinko.pauseWhileReleasing = false;
        config.presentation.showReleaseCountdown = false;
        config.cannon.firingMode = FiringMode.FramesBetweenShots;
        var m = NewModel(); m.teams[0].ammo = 8; m.Release(0);
        var ball = m.teams[0].balls[0]; ball.active = true; ball.position = new Vector2(0, 1);
        Assert.IsFalse(m.IsPlinkoPaused(0)); Assert.AreEqual(1, m.DisplayAmmo(0));
        m.Tick(.01f); Assert.AreNotEqual(new Vector2(0, 1), ball.position);
        config.plinko.pauseWhileReleasing = true;
        Assert.IsTrue(m.IsPlinkoPaused(0)); Assert.AreEqual(1, m.DisplayAmmo(0));
        config.presentation.showReleaseCountdown = true; Assert.AreEqual(8, m.DisplayAmmo(0));
        m.teams[0].queued = long.MaxValue;
        Assert.AreEqual(long.MaxValue, m.DisplayAmmo(0), "Countdown must not overflow at the storage limit.");
    }
    [Test] public void ActiveShotBudgetDefersEveryRemainingShot()
    {
        config.projectile.maxActive = 32; config.cannon.shotsPerSecond = 3000;
        var m = NewModel(); m.teams[0].ammo = 256; m.Release(0);
        for (int i = 0; i < 10; i++) m.Tick(1f / 120);
        Assert.AreEqual(32, m.shots.Count); Assert.AreEqual(224, m.teams[0].queued);
        Assert.AreEqual(256, m.teams[0].queued + m.teams[0].fired);
        Assert.IsTrue(m.IsPlinkoPaused(0)); Assert.AreEqual(224, m.DisplayAmmo(0));
    }
    [TestCase(1)] [TestCase(2)] [TestCase(5)]
    public void FrameFiringUsesPerCannonIntervalsAndNeverCatchesUp(int interval)
    {
        config.cannon.firingMode = FiringMode.FramesBetweenShots;
        config.cannon.framesBetweenShots = interval;
        var m = NewModel();
        foreach (var team in m.teams)
        {
            team.queued = 100;
            foreach (var ball in team.balls) ball.delay = 999;
        }
        for (int frame = 0; frame < 12; frame++)
        {
            long previous = m.totalFired;
            // Many catch-up simulation ticks must not fire in frame mode.
            for (int tick = 0; tick < 8; tick++) m.Tick(1f / 120);
            Assert.AreEqual(previous, m.totalFired);
            m.AdvanceFiringFrame();
            long expected = 1 + frame / interval;
            foreach (var team in m.teams)
            {
                Assert.AreEqual(expected, team.fired);
                Assert.AreEqual(100, team.fired + team.queued);
            }
        }
    }
    [Test] public void FrameFiringSharedBudgetIsFairAndDoesNotDiscardQueuedAmmo()
    {
        config.cannon.firingMode = FiringMode.FramesBetweenShots;
        config.projectile.maxSpawnsPerTick = 1;
        var m = NewModel();
        foreach (var team in m.teams) team.queued = 10;
        for (int frame = 0; frame < 8; frame++) m.AdvanceFiringFrame();
        foreach (var team in m.teams)
        {
            Assert.AreEqual(2, team.fired); Assert.AreEqual(8, team.queued);
        }
    }
    [Test] public void FrameFiringCapacityBlockDoesNotAccumulateBurstCredit()
    {
        config.cannon.firingMode = FiringMode.FramesBetweenShots;
        config.projectile.maxActive = 32;
        var m = NewModel(); m.teams[0].queued = 100;
        for (int frame = 0; frame < 100; frame++) m.AdvanceFiringFrame();
        Assert.AreEqual(32, m.totalFired); Assert.AreEqual(68, m.teams[0].queued);
        m.shots.Clear(); m.AdvanceFiringFrame();
        Assert.AreEqual(33, m.totalFired); Assert.AreEqual(1, m.shots.Count);
        m.Eliminate(0); m.AdvanceFiringFrame(); Assert.AreEqual(33, m.totalFired);
    }
    [Test] public void SecondsFiringIgnoresRenderFrameCalls()
    {
        config.cannon.firingMode = FiringMode.ShotsPerSecond;
        config.cannon.shotsPerSecond = 10;
        var m = NewModel(); m.teams[0].queued = 100;
        foreach (var team in m.teams) foreach (var ball in team.balls) ball.delay = 999;
        for (int frame = 0; frame < 8; frame++) m.AdvanceFiringFrame();
        Assert.AreEqual(0, m.totalFired);
        m.Tick(.1f); Assert.AreEqual(1, m.totalFired);
    }
    [Test] public void FrameIntervalIsValidatedAndReadyMatchesDoNotFire()
    {
        config.cannon.firingMode = FiringMode.FramesBetweenShots;
        config.cannon.framesBetweenShots = 0; config.Validate();
        Assert.AreEqual(1, config.cannon.framesBetweenShots);
        var m = new SimulationModel(config, 1234); m.teams[0].queued = 10;
        m.AdvanceFiringFrame(); Assert.AreEqual(0, m.totalFired);
        m.Start(); m.AdvanceFiringFrame(); Assert.AreEqual(1, m.totalFired);
    }
    [Test] public void StorageCeilingAndOverflowDoNotLoseOrWrapAmmo()
    {
        config.cannon.maxStoredAmmo = long.MaxValue; var m = NewModel();
        m.teams[0].ammo = long.MaxValue / 2 + 1; m.Multiply(0);
        Assert.AreEqual(long.MaxValue, m.teams[0].ammo);
        m.teams[0].queued = 1; m.Release(0);
        Assert.AreEqual(long.MaxValue, m.teams[0].ammo); Assert.AreEqual(1, m.teams[0].queued);
    }
    [Test] public void CaptureTransfersOwnershipAndPreservesTotalCellCount()
    {
        var m = NewModel(); int previous = m.owners[0]; m.Capture(0, 0, 0);
        Assert.AreEqual(0, m.owners[0]); Assert.AreEqual(1, m.teams[0].captures);
        Assert.AreEqual(m.owners.Length / 4 - 1, m.territoryCounts[previous]);
        int count = 0; foreach (int cells in m.territoryCounts) count += cells; Assert.AreEqual(m.owners.Length, count);
    }
    [TestCase(false)]
    [TestCase(true)]
    public void ProjectileDisappearsOnFirstEnemyCapture(bool bounceEnabled)
    {
        config.projectile.despawnOnCapture = true;
        config.projectile.bounceOnCapture = bounceEnabled;
        var m = NewModel();
        foreach (var team in m.teams) foreach (var ball in team.balls) ball.delay = 999;
        int x = config.board.columns / 2;
        int y = Mathf.FloorToInt((2 + config.board.size * .5f) / m.CellHeight);
        int index = y * config.board.columns + x;
        Assert.AreEqual(1, m.owners[index]);
        m.shots.Add(new ShotState { id = 999, team = 0, position = new Vector2(-.03f, 2), velocity = Vector2.right * config.projectile.speed });
        // Enough travel to cross several cells: the first capture must consume the shot.
        m.Tick(.1f);
        Assert.AreEqual(0, m.owners[index]);
        Assert.AreEqual(1, m.teams[0].captures);
        Assert.AreEqual(0, m.shots.Count);
        int count = 0; foreach (int cells in m.territoryCounts) count += cells;
        Assert.AreEqual(m.owners.Length, count);
    }
    [Test] public void ProjectileContinuesAcrossFriendlyTerritory()
    {
        var m = NewModel();
        foreach (var team in m.teams) foreach (var ball in team.balls) ball.delay = 999;
        m.shots.Add(new ShotState { id = 999, team = 0, position = new Vector2(-2, 2), velocity = Vector2.right * config.projectile.speed });
        m.Tick(.1f);
        Assert.AreEqual(1, m.shots.Count);
        Assert.Greater(m.shots[0].position.x, -2);
        Assert.AreEqual(0, m.teams[0].captures);
    }
    [Test] public void EliminatedTeamsStopPlinkoAndQueuedFireButKeepAirborneShots()
    {
        var m = NewModel(); m.Release(0); m.Tick(.02f); Assert.Greater(m.shots.Count, 0);
        int shots = m.shots.Count; m.teams[0].queued = 50; m.Eliminate(0);
        Assert.AreEqual(shots, m.shots.Count); Assert.AreEqual(0, m.teams[0].queued);
        long ammo = m.teams[0].ammo; m.Multiply(0); m.Release(0); Assert.AreEqual(ammo, m.teams[0].ammo);
    }
    [Test] public void SweptProjectileHitEliminatesEnemyCannon()
    {
        var m = NewModel(); var pos = m.teams[1].cannonPosition;
        m.shots.Add(new ShotState { id = 999, team = 0, position = pos + Vector2.left * .3f, velocity = Vector2.right * 35 });
        m.Tick(1f / 120); Assert.IsFalse(m.teams[1].alive); Assert.AreEqual(0, m.shots.Count);
    }
    [Test] public void LastSurvivorWaitsForAirborneShotsBeforeWinning()
    {
        config.resultDelay = 0; var m = NewModel();
        m.shots.Add(new ShotState { id = 999, team = 1, position = new Vector2(2, 2), velocity = Vector2.right });
        m.Eliminate(1); m.Eliminate(2); m.Eliminate(3); m.Tick(.01f);
        Assert.AreEqual(MatchPhase.Settling, m.phase); Assert.AreEqual(-1, m.winner);
        m.shots.Clear(); m.Tick(.01f); Assert.AreEqual(MatchPhase.Finished, m.phase); Assert.AreEqual(0, m.winner);
    }
    [Test] public void FinalAirborneShotCanDestroyLastCannonAndCauseDraw()
    {
        config.resultDelay = 0; var m = NewModel(); m.Eliminate(1); m.Eliminate(2); m.Eliminate(3);
        var pos = m.teams[0].cannonPosition;
        m.shots.Add(new ShotState { id = 999, team = 1, position = pos + Vector2.right * .3f, velocity = Vector2.left * 35 });
        m.Tick(1f / 120); Assert.AreEqual(MatchPhase.Finished, m.phase); Assert.AreEqual(-1, m.winner);
    }
    [Test] public void SameSeedReplaysSameGateEventsAndTerritory()
    {
        var a = NewModel(); var b = NewModel();
        for (int i = 0; i < 3600; i++) { a.Tick(1f / 120); b.Tick(1f / 120); }
        CollectionAssert.AreEqual(a.owners, b.owners); Assert.AreEqual(a.totalFired, b.totalFired);
        for (int t = 0; t < 4; t++)
        {
            Assert.AreEqual(a.teams[t].ammo, b.teams[t].ammo);
            Assert.Greater(a.teams[t].multiplies + a.teams[t].releases, 0);
            foreach (var ball in a.teams[t].balls)
            {
                Assert.IsFalse(float.IsNaN(ball.position.x)); Assert.IsFalse(float.IsNaN(ball.position.y));
                Assert.Greater(ball.cycles, 0);
            }
        }
    }
    [Test] public void TimeoutCanFinishTiedMatchAsDraw()
    {
        config.matchTimeLimit = .1f; var m = NewModel(); m.Tick(.1f);
        Assert.AreEqual(MatchPhase.Finished, m.phase); Assert.AreEqual(-1, m.winner);
    }
    [Test] public void CannonSweepRespectsAngleSpeedAndDirection()
    {
        config.teams[0].aimDegrees = 45; config.teams[0].sweepDegrees = 120; config.teams[0].sweepSpeed = 30;
        config.teams[1].aimDegrees = 45; config.teams[1].sweepDegrees = 120; config.teams[1].sweepSpeed = 30; config.teams[1].clockwise = true;
        var m = NewModel(); foreach (var team in m.teams) foreach (var ball in team.balls) ball.delay = 999;
        m.Tick(1); Assert.AreEqual(15, m.teams[0].angle, .001f); Assert.AreEqual(75, m.teams[1].angle, .001f);
        m.Tick(3); Assert.AreEqual(105, m.teams[0].angle, .001f);
        m.Tick(1); Assert.AreEqual(75, m.teams[0].angle, .001f);
    }
    [Test] public void GateContactsRecycleBallAndAwardCorrectMirroredAction()
    {
        var m = NewModel();
        foreach (var team in m.teams) foreach (var ball in team.balls) ball.delay = 999;
        var left = m.teams[0].balls[0]; left.active = true;
        left.position = new Vector2(-config.plinko.width * .25f, -config.plinko.height * .5f + .3f); left.velocity = Vector2.down;
        var right = m.teams[1].balls[0]; right.active = true;
        right.position = new Vector2(config.plinko.width * .25f, -config.plinko.height * .5f + .3f); right.velocity = Vector2.down;
        m.Tick(1f / 120);
        Assert.AreEqual(2, m.teams[0].ammo); Assert.AreEqual(2, m.teams[1].ammo);
        Assert.AreEqual(1, left.cycles); Assert.AreEqual(1, right.cycles);
        Assert.IsFalse(left.active); Assert.Greater(left.position.y, 0);
    }
    [Test] public void ReferenceFiringRatesCanCompleteAnEntireMatch()
    {
        var preset = UnityEditor.AssetDatabase.LoadAssetAtPath<SimulationConfig>("Assets/MultiplyOrRelease/Config/DefaultSimulation.asset");
        Assert.IsNotNull(preset, "Create the simulation scene before running integration tests.");
        var copy = UnityEngine.Object.Instantiate(preset);
        try
        {
            // Inspector firing/sweep tuning is user data, not a completion-time
            // contract. Use known reference rates on a copy; never edit the asset.
            copy.randomSeed = 1207;
            copy.cannon.firingMode = FiringMode.ShotsPerSecond;
            copy.cannon.shotsPerSecond = 180;
            copy.projectile.speed = 6.5f;
            copy.plinko.pauseWhileReleasing = true;
            foreach (var team in copy.teams) team.sweepSpeed = 38;
            var m = new SimulationModel(copy, copy.randomSeed); m.Start();
            for (int i = 0; i < 144000 && m.phase != MatchPhase.Finished; i++)
            {
                m.Tick(1f / copy.ticksPerSecond);
                // Emulate a 60 FPS player at the default 120 fixed ticks/second.
                if (i % 2 == 1) m.AdvanceFiringFrame();
            }
            Assert.AreEqual(MatchPhase.Finished, m.phase, "Reference firing rates should finish within 20 simulated minutes.");
            Assert.LessOrEqual(m.AliveCount, 1); Assert.Greater(m.totalFired, 0);
            int cells = 0; foreach (int count in m.territoryCounts) { Assert.GreaterOrEqual(count, 0); cells += count; }
            Assert.AreEqual(m.owners.Length, cells);
        }
        finally { UnityEngine.Object.DestroyImmediate(copy); }
    }
}
