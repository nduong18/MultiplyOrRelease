using MultiplyOrRelease;
using NUnit.Framework;
using UnityEngine;

public class SimulationBorderCollisionTests
{
    SimulationConfig config;
    SimulationModel model;

    [SetUp] public void Setup()
    {
        config = ScriptableObject.CreateInstance<SimulationConfig>();
        config.boosts.enabled = false;
        config.cannon.firingMode = FiringMode.FramesBetweenShots;
        model = new SimulationModel(config, 1234); model.Start();
        foreach (var team in model.teams) foreach (var ball in team.balls) ball.delay = 9999;
        // Isolate border contacts from territory/cannon contacts without killing teams.
        for (int y = 0; y < config.board.rows; y++)
            for (int x = 0; x < config.board.columns; x++) model.Capture(x, y, 0);
    }

    [TearDown] public void Cleanup() { Object.DestroyImmediate(config); }

    ShotState Shot(Vector2 position, Vector2 velocity)
    {
        var shot = new ShotState { id = model.shots.Count + 1, team = 0, position = position, velocity = velocity };
        model.shots.Add(shot); return shot;
    }

    void HitBorder(ShotState shot, int contact)
    {
        float direction = contact % 2 == 0 ? 1 : -1;
        float limit = config.board.size * .5f - config.projectile.radius;
        shot.position = new Vector2(direction * (limit - .001f), 1);
        shot.velocity = new Vector2(direction, 0);
        model.Tick(.01f);
    }

    [Test] public void TenBorderContactsSurviveAndEleventhRemovesOnlyThatBullet()
    {
        var shot = Shot(Vector2.zero, Vector2.zero);
        var other = Shot(new Vector2(-1, 1), Vector2.zero);
        for (int i = 0; i < 10; i++)
        {
            HitBorder(shot, i);
            Assert.Contains(shot, model.shots);
            Assert.AreEqual(i + 1, shot.borderCollisions);
            Assert.AreEqual(i % 2 == 0 ? -1 : 1, shot.velocity.x);
        }
        HitBorder(shot, 10);
        Assert.AreEqual(11, shot.borderCollisions);
        Assert.AreEqual(1, model.shots.Count);
        Assert.AreSame(other, model.shots[0]);
        Assert.AreEqual(0, other.borderCollisions);
    }

    [TestCase(1)] [TestCase(-1)]
    public void VerticalBordersCountAndMovingAwayDoesNotCountAgain(int direction)
    {
        float limit = config.board.size * .5f - config.projectile.radius;
        var shot = Shot(new Vector2(1, direction * (limit - .001f)), Vector2.up * direction);
        model.Tick(.01f);
        Assert.AreEqual(1, shot.borderCollisions);
        Assert.AreEqual(-direction, shot.velocity.y);
        for (int i = 0; i < 10; i++) model.Tick(.01f);
        Assert.AreEqual(1, shot.borderCollisions);
    }

    [Test] public void CornerReflectsBothAxesButCountsOneContact()
    {
        float limit = config.board.size * .5f - config.projectile.radius;
        var shot = Shot(Vector2.one * (limit - .001f), Vector2.one);
        model.Tick(.01f);
        Assert.AreEqual(1, shot.borderCollisions);
        Assert.AreEqual(-1, shot.velocity.x); Assert.AreEqual(-1, shot.velocity.y);
    }

    [Test] public void ContinuousTravelCountsEveryBounceAcrossTicks()
    {
        config.board.size = 4;
        config.projectile.lifeTime = 120;
        var shot = Shot(new Vector2(0, .5f), Vector2.right * 35);
        int frames = 0;
        while (model.shots.Count > 0 && frames++ < 1000) model.Tick(1f / 120);
        Assert.AreEqual(0, model.shots.Count);
        Assert.AreEqual(11, shot.borderCollisions);
        Assert.Less(shot.age, config.projectile.lifeTime);
    }

    [Test] public void EnemyGridStillCapturesAndRemovesImmediatelyWithoutBorderCredit()
    {
        var shot = Shot(new Vector2(1, 1), Vector2.zero);
        int x = Mathf.FloorToInt((shot.position.x + config.board.size * .5f) / model.CellWidth);
        int y = Mathf.FloorToInt((shot.position.y + config.board.size * .5f) / model.CellHeight);
        model.Capture(x, y, 1);
        model.Tick(.01f);
        Assert.AreEqual(0, model.shots.Count);
        Assert.AreEqual(0, shot.borderCollisions);
        Assert.AreEqual(0, model.owners[y * config.board.columns + x]);
    }

    [Test] public void OptionalGridBouncesNeverIncreaseBorderCount()
    {
        config.projectile.despawnOnCapture = false;
        config.projectile.bounceOnCapture = true;
        var shot = Shot(new Vector2(1, 1), Vector2.zero);
        int x = Mathf.FloorToInt((1 + config.board.size * .5f) / model.CellWidth);
        int y = Mathf.FloorToInt((1 + config.board.size * .5f) / model.CellHeight);
        for (int i = 0; i < 12; i++)
        {
            model.Capture(x, y, 1); model.Tick(.01f);
            Assert.AreEqual(1, model.shots.Count);
        }
        Assert.AreEqual(0, shot.borderCollisions);
    }

    [Test] public void ReusedProjectileStartsWithZeroBorderContacts()
    {
        var shot = Shot(Vector2.zero, Vector2.zero);
        shot.borderCollisions = 10; HitBorder(shot, 0);
        Assert.AreEqual(0, model.shots.Count);
        model.teams[0].queued = 1; model.AdvanceFiringFrame();
        Assert.AreEqual(1, model.shots.Count);
        Assert.AreSame(shot, model.shots[0], "Exercise the actual shot pool.");
        Assert.AreEqual(0, model.shots[0].borderCollisions);
    }

    [Test] public void CustomLimitAndDisabledLimitAreRespected()
    {
        config.projectile.maxBorderCollisions = 2;
        var shot = Shot(Vector2.zero, Vector2.zero);
        HitBorder(shot, 0); HitBorder(shot, 1);
        Assert.AreEqual(1, model.shots.Count);
        HitBorder(shot, 2); Assert.AreEqual(0, model.shots.Count);
        config.projectile.maxBorderCollisions = 0;
        var unlimited = Shot(Vector2.zero, Vector2.zero);
        for (int i = 0; i < 20; i++) HitBorder(unlimited, i);
        Assert.AreEqual(20, unlimited.borderCollisions); Assert.AreEqual(1, model.shots.Count);
    }

    [Test] public void DisablingBorderBounceStillRemovesAtFirstBorder()
    {
        config.projectile.bounceAtArenaEdge = false;
        var shot = Shot(Vector2.zero, Vector2.zero); HitBorder(shot, 0);
        Assert.AreEqual(0, model.shots.Count);
    }
}
