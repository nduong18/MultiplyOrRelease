using System.Linq;
using MultiplyOrRelease;
using NUnit.Framework;
using UnityEngine;

public class SimulationCannonImpactTests
{
    SimulationConfig config;
    SimulationModel model;
    GameObject host;
    CannonImpactVfx effect;

    [SetUp] public void Setup()
    {
        config = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<SimulationConfig>(
            "Assets/MultiplyOrRelease/Config/DefaultSimulation.asset"));
        config.cannonImpact = new CannonImpactSettings();
        config.boosts.enabled = false;
        config.matchTimeLimit = 0;
        model = new SimulationModel(config, 1234);
        foreach (var team in model.teams) foreach (var ball in team.balls) ball.delay = 9999;
        model.Start();
        host = new GameObject("Cannon Impact Test");
        effect = new CannonImpactVfx(host.transform, model);
    }

    [TearDown] public void Cleanup()
    {
        effect?.Dispose(); effect = null;
        Object.DestroyImmediate(host); Object.DestroyImmediate(config);
    }

    void Hit(int shooter = 0, int target = 1)
    {
        model.shots.Add(new ShotState { id = 999, team = shooter, position = model.teams[target].cannonPosition });
        model.Tick(0);
    }

    [TestCase(false)] [TestCase(true)]
    public void DamagingAndLethalHitsEmitOneExplosionAtTargetFlag(bool lethal)
    {
        config.cannon.destroyOnEnemyHit = lethal;
        model.teams[1].health = 2;
        int contacts = 0;
        model.CannonHit += (target, shooter, destroyed) =>
        {
            contacts++;
            Assert.AreEqual(1, target); Assert.AreEqual(0, shooter); Assert.AreEqual(lethal, destroyed);
        };
        Hit();
        Assert.AreEqual(1, contacts);
        Assert.AreEqual(!lethal, model.teams[1].alive);
        Assert.AreEqual(0, model.shots.Count);
        Assert.AreEqual(1, effect.ActiveCount);
        var explosion = host.transform.Find("Cannon Impact VFX/Cannon Explosion");
        Assert.AreEqual((Vector3)model.teams[1].cannonPosition, explosion.localPosition);
        Assert.IsTrue(explosion.gameObject.activeInHierarchy, "The explosion survives hiding a destroyed cannon.");
        model.Tick(0); Assert.AreEqual(1, contacts);
    }

    [Test] public void FriendlyCannonGridHitsAndAdministrativeEliminationDoNotExplode()
    {
        Hit(1, 1);
        Assert.AreEqual(0, effect.ActiveCount);
        model.shots.Clear();
        model.shots.Add(new ShotState { team = 0, position = new Vector2(1, 1) }); model.Tick(0);
        Assert.AreEqual(0, effect.ActiveCount);
        model.Eliminate(1);
        Assert.AreEqual(0, effect.PoolCount);
    }

    [Test] public void DisablingVfxKeepsDamageAndProjectileRemoval()
    {
        config.cannonImpact.enabled = false;
        Hit();
        Assert.IsFalse(model.teams[1].alive);
        Assert.AreEqual(0, model.shots.Count);
        Assert.AreEqual(0, effect.PoolCount);
    }

    [Test] public void BurstPausesFadesAndRecyclesWithinPoolLimit()
    {
        config.cannonImpact.maxBursts = 4;
        for (int i = 0; i < 12; i++) effect.Burst(i % 4, (i + 1) % 4, true);
        Assert.AreEqual(4, effect.ActiveCount);
        Assert.AreEqual(4, effect.PoolCount);
        var spark = host.GetComponentsInChildren<SpriteRenderer>().First(r => r.name == "Spark");
        var flash = host.GetComponentsInChildren<SpriteRenderer>().First(r => r.name == "Explosion Flash");
        Vector3 start = spark.transform.localPosition;
        float flashAlpha = flash.color.a;
        effect.Advance(0);
        Assert.AreEqual(start, spark.transform.localPosition);
        Assert.AreEqual(flashAlpha, flash.color.a);
        effect.Advance(config.cannonImpact.duration * .5f);
        Assert.AreNotEqual(start, spark.transform.localPosition);
        Assert.Less(spark.color.a, 1);
        Assert.AreEqual(0, flash.color.a);
        effect.Advance(config.cannonImpact.duration);
        Assert.AreEqual(0, effect.ActiveCount);
        effect.Burst(1, 0, false);
        Assert.AreEqual(1, effect.ActiveCount);
        Assert.AreEqual(4, effect.PoolCount);
        Assert.AreEqual(1, host.GetComponentsInChildren<SpriteRenderer>().First(r => r.name == "Explosion Flash").color.a);
    }

    [Test] public void VisualRandomnessDoesNotChangeGameplayAndDisposedEffectsUnsubscribe()
    {
        config.cannon.firingMode = FiringMode.ShotsPerSecond;
        config.cannon.shotsPerSecond = 20;
        var other = new SimulationModel(config, 1234); other.Start();
        foreach (var team in other.teams) foreach (var ball in team.balls) ball.delay = 9999;
        effect.Burst(0, 1, false);
        model.teams[0].queued = other.teams[0].queued = 10;
        for (int i = 0; i < 10; i++) { model.Tick(.01f); other.Tick(.01f); }
        Assert.Greater(model.shots.Count, 0);
        CollectionAssert.AreEqual(other.shots.Select(s => s.velocity), model.shots.Select(s => s.velocity));
        CollectionAssert.AreEqual(other.shots.Select(s => s.position), model.shots.Select(s => s.position));
        effect.Dispose(); effect.Dispose();
        Hit();
        Assert.AreEqual(0, effect.ActiveCount);
        Assert.AreEqual(0, host.transform.childCount);
    }

    [Test] public void ViewKeepsExplosionVisibleAfterEliminationAndDisposesIt()
    {
        effect.Dispose();
        var view = new SimulationView(host.transform, model, true);
        try
        {
            Hit(); view.Render();
            var explosion = host.transform.Find("Generated Preview/Cannon Impact VFX/Cannon Explosion");
            Assert.IsTrue(explosion.gameObject.activeInHierarchy);
            view.AdvanceEffects(config.cannonImpact.duration + .01f);
            Assert.IsFalse(explosion.gameObject.activeInHierarchy);
        }
        finally { view.Dispose(); }
        Assert.AreEqual(0, host.transform.childCount);
    }
}
