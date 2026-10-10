using MultiplyOrRelease;
using NUnit.Framework;
using UnityEngine;

public class ConquestModelTests
{
    ConquestConfig config;
    ConquestModel model;
    [SetUp] public void Setup()
    {
        config = ScriptableObject.CreateInstance<ConquestConfig>(); config.Validate();
        model = new ConquestModel(config, 24);
    }
    [TearDown] public void Cleanup() => Object.DestroyImmediate(config);

    [Test] public void InitializesTwentyFourCountriesInSixByFourOrder()
    {
        Assert.AreEqual(24, model.CountryCount); Assert.AreEqual(24, model.cannons.Length);
        for (int i = 0; i < 24; i++)
        {
            Assert.AreEqual(i, model.cannons[i].owner); Assert.AreEqual(1, model.cannons[i].ammo);
            Vector2 position = model.cannons[i].position;
            int x = Mathf.FloorToInt((position.x / config.arenaSize.x + .5f) * config.GridColumns);
            int y = Mathf.FloorToInt((position.y / config.arenaSize.y + .5f) * config.GridRows);
            Assert.AreEqual(i, model.owners[y * config.GridColumns + x]);
        }
        Assert.Greater(model.cannons[0].position.y, model.cannons[18].position.y);
        Assert.Less(model.cannons[0].position.x, model.cannons[5].position.x);
    }
    [Test] public void CaptureChangesOwnerWithoutDestroyingSlotAndCancelsOldVolley()
    {
        var target = model.cannons[7]; Vector2 position = target.position; var ball = target.balls[0];
        model.Multiply(7); model.Release(7); model.CaptureCannon(7, 0);
        Assert.AreSame(target, model.cannons[7]); Assert.AreEqual(position, target.position);
        Assert.AreSame(ball, target.balls[0]); Assert.AreEqual(0, target.owner);
        Assert.AreEqual(config.ammoAfterCapture, target.ammo); Assert.AreEqual(0, target.queued);
        Assert.AreEqual(23, model.CountryCount); Assert.AreEqual(24, model.cannons.Length);
    }
    [Test] public void MultiplyAndReleaseOperateOnSlotBanksAfterCapture()
    {
        model.CaptureCannon(4, 0); model.Multiply(4); model.Multiply(4); model.Release(4);
        Assert.AreEqual(4, model.cannons[4].queued); Assert.AreEqual(1, model.cannons[4].ammo);
        Assert.AreEqual(1, model.cannons[0].ammo); Assert.AreEqual(0, model.cannons[0].queued);
        model.Tick(.1f); Assert.Greater(model.totalFired, 0);
        foreach (var shot in model.shots) Assert.AreEqual(0, shot.team);
    }
    [Test] public void EliminatedCountryCannotReappearFromAirborneShots()
    {
        model.shots.Add(new ShotState { id = 91, team = 7 }); model.CaptureCannon(7, 0);
        Assert.AreEqual(0, model.shots.Count); model.CaptureCannon(8, 7);
        Assert.AreEqual(8, model.cannons[8].owner);
    }
    [Test] public void CapturedCountryRetainsItsShotsWhileItOwnsAnotherCannon()
    {
        model.CaptureCannon(8, 7); model.shots.Add(new ShotState { id = 91, team = 7 });
        model.CaptureCannon(7, 0); Assert.AreEqual(1, model.shots.Count); Assert.IsTrue(model.OwnsCannon(7));
        model.CaptureCannon(8, 0); Assert.AreEqual(0, model.shots.Count);
    }
    [Test] public void OnlyOneCannonOwnerLocksWinnerExactlyOnceRegardlessOfTerritory()
    {
        int events = 0; model.Won += owner => { Assert.AreEqual(0, owner); events++; };
        for (int i = 1; i < 23; i++) model.CaptureCannon(i, 0);
        Assert.IsFalse(model.Finished); Assert.AreEqual(2, model.CountryCount);
        model.CaptureCannon(23, 0); Assert.IsTrue(model.Finished); Assert.AreEqual(0, model.winner); Assert.AreEqual(1, events);
        float elapsed = model.elapsed; model.Tick(1); model.CaptureCannon(0, 23);
        Assert.AreEqual(elapsed, model.elapsed); Assert.AreEqual(0, model.winner); Assert.AreEqual(1, events);
        Assert.Contains(23, model.owners); // Cannon ownership, not a full grid repaint, determines victory.
    }
    [Test] public void MovingObstaclesRiseAndWrapContinuously()
    {
        float original = model.pegs[0].y, x = model.pegs[0].x;
        model.Tick(.1f); Assert.AreEqual(original + config.obstacleRiseSpeed * .1f, model.pegs[0].y, .0001f);
        Assert.AreEqual(x, model.pegs[0].x);
        float height = config.arenaSize.y - config.plinko.gateHeight - config.plinko.pegBottomInset - config.plinko.pegTopInset;
        model.Tick(height / config.obstacleRiseSpeed - .1f); Assert.AreEqual(original, model.pegs[0].y, .001f);
    }
    [Test] public void FastProjectileSweepsThroughCannonAndTransfersOwnership()
    {
        config.projectile.despawnOnCapture = false;
        var position = model.cannons[7].position;
        model.shots.Add(new ShotState { id = 91, team = 0, position = position - Vector2.right,
            velocity = Vector2.right * 20 });
        model.Tick(.1f); Assert.AreEqual(0, model.cannons[7].owner); Assert.AreEqual(1, model.captureCount);
        Assert.AreEqual(0, model.shots.Count);
    }
    [Test] public void FriendlyCannonDoesNotConvertOrConsumeProjectile()
    {
        config.projectile.despawnOnCapture = false; var position = model.cannons[0].position;
        model.shots.Add(new ShotState { id = 91, team = 0, position = position - Vector2.right * .5f, velocity = Vector2.right * 5 });
        model.Tick(.1f); Assert.AreEqual(0, model.captureCount); Assert.AreEqual(1, model.shots.Count);
    }
    [Test] public void AmmoSaturatesWithoutOverflow()
    {
        config.cannon.maxStoredAmmo = long.MaxValue;
        for (int i = 0; i < 80; i++) model.Multiply(0);
        Assert.AreEqual(long.MaxValue, model.cannons[0].ammo);
        model.Release(0); model.cannons[0].ammo = long.MaxValue; model.Release(0);
        Assert.AreEqual(long.MaxValue, model.cannons[0].queued);
    }
    [Test] public void FrameBasedFiringIsBoundedByRenderedFrames()
    {
        config.cannon.firingMode = FiringMode.FramesBetweenShots; config.cannon.framesBetweenShots = 2;
        model.cannons[0].queued = 10; model.Tick(.1f); Assert.AreEqual(0, model.totalFired);
        model.AdvanceFiringFrame(); model.AdvanceFiringFrame(); Assert.AreEqual(1, model.totalFired);
        model.AdvanceFiringFrame(); Assert.AreEqual(2, model.totalFired);
    }

    [TestCase(false)] [TestCase(true)]
    public void PairSeparationCannotPushMarbleThroughEitherWall(bool rightWall)
    {
        config.plinko.ballRadius = .24f; config.plinko.pegRadius = .12f; config.plinko.rows = 5;
        config.obstacleRiseSpeed = 0; model = new ConquestModel(config, 24);
        foreach (var cannon in model.cannons) foreach (var ball in cannon.balls)
        { ball.active = false; ball.delay = 100000; }
        float left = config.PlinkoLeft + config.plinko.wallInset + config.plinko.ballRadius;
        float right = config.PlinkoLeft + config.plinko.width - config.plinko.wallInset - config.plinko.ballRadius;
        float direction = rightWall ? -1 : 1;
        float wall = rightWall ? right : left;
        var a = model.cannons[0].balls[0]; var b = model.cannons[1].balls[0];
        a.active = b.active = true;
        a.position = new Vector2(wall, -3); b.position = a.position + Vector2.right * direction * .15f;
        a.velocity = b.velocity = Vector2.zero;
        for (int tick = 0; tick < 120; tick++)
        {
            model.Tick(1f / 120);
            Assert.That(a.position.x, Is.InRange(left - .00001f, right + .00001f));
            Assert.That(b.position.x, Is.InRange(left - .00001f, right + .00001f));
        }
    }
    [TestCase(.18f, .12f, 5)] [TestCase(.24f, .12f, 5)] [TestCase(.24f, .18f, 12)]
    public void LargeMarblesStayInsideWallsAndClearPegsThroughRepeatedCycles(float radius, float pegRadius, int rows)
    {
        config.plinko.ballRadius = radius; config.plinko.pegRadius = pegRadius; config.plinko.rows = rows;
        config.obstacleRiseSpeed = .8f; model = new ConquestModel(config, 24);
        float left = config.PlinkoLeft + config.plinko.wallInset + radius;
        float right = config.PlinkoLeft + config.plinko.width - config.plinko.wallInset - radius;
        float top = config.arenaSize.y * .5f - radius;
        float contact = radius + pegRadius;
        for (int tick = 0; tick < 120 * 30; tick++)
        {
            model.Tick(1f / 120);
            foreach (var cannon in model.cannons) foreach (var ball in cannon.balls)
            {
                if (!ball.active) continue;
                Assert.That(ball.position.x, Is.InRange(left - .00001f, right + .00001f));
                Assert.LessOrEqual(ball.position.y, top + .00001f);
                foreach (var peg in model.pegs)
                    Assert.GreaterOrEqual((ball.position - peg).sqrMagnitude, (contact - .001f) * (contact - .001f));
            }
        }
    }
    [TestCase(.18f)] [TestCase(.24f)]
    public void FullPegRowsLeaveAMarbleSizedPassageAtBothWalls(float radius)
    {
        config.plinko.ballRadius = radius; config.plinko.pegRadius = .12f;
        model = new ConquestModel(config, 24);
        float wallLeft = config.PlinkoLeft + config.plinko.wallInset;
        float wallRight = config.PlinkoLeft + config.plinko.width - config.plinko.wallInset;
        Assert.Greater(model.pegs[0].x - config.plinko.pegRadius - wallLeft, radius * 2);
        Assert.Greater(wallRight - model.pegs[config.plinko.columns - 1].x - config.plinko.pegRadius, radius * 2);
        Assert.AreEqual(wallLeft, model.pegs[config.plinko.columns].x, .00001f);
        Assert.AreEqual(wallRight, model.pegs[config.plinko.columns * 2 - 1].x, .00001f);
    }
}
