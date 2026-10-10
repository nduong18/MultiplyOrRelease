using MultiplyOrRelease;
using NUnit.Framework;
using UnityEngine;

public class ConquestPlinkoBoostTests
{
    ConquestConfig config;
    ConquestModel model;
    PlinkoBall ball;
    Vector2 center;
    [SetUp] public void Setup()
    {
        config = ScriptableObject.CreateInstance<ConquestConfig>();
        config.obstacleRiseSpeed = 0; config.plinko.collideBalls = false;
        config.plinko.pegTopInset = 6; config.plinko.rows = 2;
        config.plinko.gravity = .1f;
        config.plinkoBoosts = new[] { new ConquestPlinkoBoost { kind = PlinkoBoostKind.MultiplyAmmo,
            position = new Vector2(.5f, .82f) } };
        model = new ConquestModel(config, 24);
        foreach (var cannon in model.cannons) foreach (var other in cannon.balls)
        { other.active = false; other.delay = 10000; }
        ball = model.cannons[7].balls[0]; ball.active = true;
        center = model.PlinkoBoostPosition(0); ball.position = center; ball.velocity = Vector2.zero;
    }
    [TearDown] public void Cleanup() => Object.DestroyImmediate(config);

    [Test] public void AmmoBoostRewardsOnlyTheCollectingSlotAfterCaptureAndSaturates()
    {
        config.cannon.maxStoredAmmo = long.MaxValue; config.plinkoBoosts[0].ammoMultiplier = int.MaxValue;
        model.CaptureCannon(7, 0); model.cannons[7].ammo = long.MaxValue / 2;
        model.cannons[7].queued = 9; model.Tick(1f / 120);
        Assert.AreEqual(long.MaxValue, model.cannons[7].ammo);
        Assert.AreEqual(1, model.cannons[0].ammo); Assert.AreEqual(9, model.cannons[7].queued);
        Assert.AreEqual(1, model.plinkoBoostCollectionCount);
    }
    [Test] public void StayingInTheZoneCannotCollectAgainBeforeRespawn()
    {
        for (int i = 0; i < 60; i++) { ball.position = center; ball.velocity = Vector2.zero; model.Tick(1f / 120); }
        Assert.AreEqual(2, model.cannons[7].ammo); Assert.AreEqual(1, model.plinkoBoostCollectionCount);
        ball.position = center + Vector2.down; model.Tick(.2f);
        ball.position = center; model.Tick(1f / 120);
        Assert.AreEqual(2, model.cannons[7].ammo); Assert.AreEqual(1, model.plinkoBoostCollectionCount);
    }
    [Test] public void OnlyOneOfTwoMarblesCanCollectInTheSameTick()
    {
        var other = model.cannons[8].balls[0]; other.active = true;
        other.position = center; other.velocity = Vector2.zero;
        model.Tick(1f / 120);
        Assert.AreEqual(1, model.plinkoBoostCollectionCount);
        Assert.AreEqual(2, model.cannons[7].ammo); Assert.AreEqual(1, model.cannons[8].ammo);
        Assert.IsFalse(model.IsPlinkoBoostAvailable(0));
    }
    [Test] public void BoostReturnsAfterTenSecondsAndAnotherMarbleCanCollect()
    {
        model.Tick(.01f); ball.active = false; ball.delay = 10000;
        Assert.IsFalse(model.IsPlinkoBoostAvailable(0));
        model.Tick(9.99f); Assert.IsFalse(model.IsPlinkoBoostAvailable(0));
        model.Tick(.011f); Assert.IsTrue(model.IsPlinkoBoostAvailable(0));
        var other = model.cannons[8].balls[0]; other.active = true;
        other.position = center; other.velocity = Vector2.zero;
        model.Tick(.01f);
        Assert.AreEqual(2, model.cannons[8].ammo); Assert.AreEqual(2, model.plinkoBoostCollectionCount);
        Assert.IsFalse(model.IsPlinkoBoostAvailable(0));
        var restarted = new ConquestModel(config, 24); Assert.IsTrue(restarted.IsPlinkoBoostAvailable(0));
    }
    [Test] public void CollectingOneBoostLeavesOtherBoostsAvailable()
    {
        config.plinkoBoosts = new[] { config.plinkoBoosts[0], new ConquestPlinkoBoost { position = new Vector2(.2f, .2f) } };
        model = new ConquestModel(config, 24);
        foreach (var cannon in model.cannons) foreach (var other in cannon.balls)
        { other.active = false; other.delay = 10000; }
        ball = model.cannons[7].balls[0]; ball.active = true; ball.position = center; ball.velocity = Vector2.zero;
        model.Tick(.01f);
        Assert.IsFalse(model.IsPlinkoBoostAvailable(0)); Assert.IsTrue(model.IsPlinkoBoostAvailable(1));
    }
    [Test] public void FastMarbleCollectsWhenItsSegmentCrossesTheZone()
    {
        ball.position = center + Vector2.up * .55f; ball.velocity = Vector2.down * 12;
        model.Tick(.1f);
        Assert.Greater(Vector2.Distance(ball.position, center), config.plinkoBoosts[0].radius + config.plinko.ballRadius);
        Assert.AreEqual(2, model.cannons[7].ammo);
    }
    [Test] public void SpeedBoostAcceleratesDownwardAndClampsMagnitude()
    {
        config.plinkoBoosts[0].kind = PlinkoBoostKind.MarbleSpeed;
        config.plinkoBoosts[0].maxSpeed = 5; config.plinkoBoosts[0].minimumFallSpeed = 4;
        ball.velocity = new Vector2(8, 2); model.Tick(1f / 120);
        Assert.Less(ball.velocity.y, 0); Assert.LessOrEqual(ball.velocity.magnitude, 5.00001f);
        Assert.AreEqual(1, model.cannons[7].ammo); Assert.AreEqual(1, model.plinkoBoostCollectionCount);
    }
    [TestCase(false)] [TestCase(true)]
    public void DisabledBoostsAndWaitingMarblesCannotCollect(bool waiting)
    {
        if (waiting) { ball.active = false; ball.delay = 2; }
        else config.enablePlinkoBoosts = false;
        model.Tick(.1f);
        Assert.AreEqual(1, model.cannons[7].ammo); Assert.AreEqual(0, model.plinkoBoostCollectionCount);
    }
    [Test] public void PausedReleaseDoesNotCollectAndRestartClearsBoostHistory()
    {
        config.plinko.pauseWhileReleasing = true; model.cannons[7].queued = 100;
        model.Tick(.1f); Assert.AreEqual(0, model.plinkoBoostCollectionCount);
        model.cannons[7].queued = 0; model.Tick(.1f); Assert.AreEqual(1, model.plinkoBoostCollectionCount);
        var restarted = new ConquestModel(config, 24); Assert.AreEqual(0, restarted.plinkoBoostCollectionCount);
    }
    [Test] public void RecycledMarbleDoesNotSweepAcrossBoostOnTeleport()
    {
        ball.position = new Vector2(center.x, -4.8f); model.Tick(1f / 120);
        Assert.IsFalse(ball.active); Assert.AreEqual(0, model.plinkoBoostCollectionCount);
        ball.position = new Vector2(center.x, 4.6f); ball.velocity = Vector2.zero;
        for (int i = 0; i < 30; i++) model.Tick(1f / 120);
        Assert.AreEqual(0, model.plinkoBoostCollectionCount);
    }
}
