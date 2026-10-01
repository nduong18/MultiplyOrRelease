using System;
using MultiplyOrRelease;
using NUnit.Framework;
using UnityEngine;

public class SimulationGridImpactTests
{
    SimulationConfig config;
    SimulationModel model;
    GameObject parent;
    GridImpactVfx effect;

    [SetUp] public void Setup()
    {
        config = UnityEngine.Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<SimulationConfig>(
            "Assets/MultiplyOrRelease/Config/DefaultSimulation.asset"));
        model = new SimulationModel(config, 1234);
        foreach (var team in model.teams) foreach (var ball in team.balls) ball.delay = 999;
        parent = new GameObject("Grid Impact Test");
        effect = new GridImpactVfx(parent.transform, model);
        model.Start();
    }

    [TearDown] public void Cleanup()
    {
        effect?.Dispose();
        UnityEngine.Object.DestroyImmediate(parent);
        UnityEngine.Object.DestroyImmediate(config);
    }

    ShotState Shot(int team, Vector2 position)
    {
        var shot = new ShotState { team = team, id = 1, position = position };
        model.shots.Add(shot); return shot;
    }

    [TestCase(0)] [TestCase(2)]
    public void GridHitEmitsOnceEvenWhenCaptureChangesSeveralCellsAndBulletDisappears(int radius)
    {
        config.projectile.captureRadiusCells = radius;
        config.projectile.despawnOnCapture = true;
        int contacts = 0; Vector2 hit = Vector2.zero; int hitter = -1;
        model.GridHit += (p, t) => { contacts++; hit = p; hitter = t; };
        var position = new Vector2(1, 1); Shot(0, position);
        model.Tick(1f / 120);
        Assert.AreEqual(1, contacts); Assert.AreEqual(position, hit); Assert.AreEqual(0, hitter);
        Assert.AreEqual(0, model.shots.Count);
        Assert.AreEqual(1, effect.ActiveCount);
        model.Tick(1f / 120); Assert.AreEqual(1, contacts);
    }

    [Test] public void FriendlyGridAndAdministrativeCapturesDoNotEmitImpact()
    {
        Shot(0, new Vector2(-1, 1));
        model.Tick(1f / 120);
        Assert.AreEqual(0, effect.ActiveCount);
        model.Capture(40, 40, 0);
        Assert.AreEqual(0, effect.ActiveCount);
    }

    [Test] public void CanDisableVfxWithoutChangingCaptureAndProjectileRemoval()
    {
        config.gridImpact.enabled = false; Shot(0, new Vector2(1, 1));
        model.Tick(1f / 120);
        Assert.Greater(model.teams[0].captures, 0); Assert.AreEqual(0, model.shots.Count);
        Assert.AreEqual(0, effect.PoolCount);
    }

    [Test] public void CirclesUseTeamColorDrawAboveGridAndHaveNoTrails()
    {
        config.teams[1].projectileColor = Color.green;
        effect.Burst(new Vector2(1, 2), 1);
        var circles = Array.FindAll(parent.GetComponentsInChildren<SpriteRenderer>(), r => r.name == "Circle");
        Assert.AreEqual(1, circles.Length);
        foreach (var r in circles)
        {
            Assert.AreSame(config.presentation.circleSprite, r.sprite);
            Assert.AreSame(config.presentation.spriteMaterial, r.sharedMaterial);
            Assert.AreEqual(config.gridImpact.sortingOrder, r.sortingOrder);
            Assert.Greater(r.color.g, .99f); Assert.Less(r.color.r, .13f);
            Assert.AreEqual(config.gridImpact.opacity, r.color.a, .0001f);
            Assert.AreEqual(r.bounds.size.x, r.bounds.size.y, .0001f);
        }
        Assert.AreEqual(0, parent.GetComponentsInChildren<TrailRenderer>().Length);
    }

    [Test] public void CustomColorSizeAndLifetimeAreHonoredAndPoolIsBoundedAndReused()
    {
        var s = config.gridImpact;
        s.useTeamColor = false; s.color = Color.magenta; s.maxParticles = 32;
        for (int i = 0; i < 100; i++) effect.Burst(Vector2.zero, 0);
        Assert.AreEqual(32, effect.ActiveCount); Assert.AreEqual(32, effect.PoolCount);
        var circle = Array.Find(parent.GetComponentsInChildren<SpriteRenderer>(), r => r.name == "Circle");
        Assert.AreEqual(1, circle.color.r); Assert.AreEqual(1, circle.color.b);
        float cell = Mathf.Min(model.CellWidth, model.CellHeight);
        Assert.That(circle.bounds.size.x, Is.InRange(cell * s.sizeInCells * .65f, cell * s.sizeInCells * 1.15f));
        Vector3 before = circle.transform.parent.localPosition;
        effect.Advance(0); Assert.AreEqual(before, circle.transform.parent.localPosition);
        effect.Advance(s.duration * .5f);
        Assert.AreNotEqual(before, circle.transform.parent.localPosition);
        Assert.Less(circle.color.a, s.opacity);
        effect.Advance(s.duration);
        Assert.AreEqual(0, effect.ActiveCount);
        effect.Burst(Vector2.one, 0);
        Assert.AreEqual(1, effect.ActiveCount); Assert.AreEqual(32, effect.PoolCount);
        effect.Dispose(); effect.Dispose();
        Assert.AreEqual(0, parent.transform.childCount);
        Shot(0, new Vector2(1, 1)); model.Tick(1f / 120);
        Assert.AreEqual(0, effect.ActiveCount, "Disposed effects must unsubscribe from the model.");
    }

    [Test] public void ViewAutomaticallySubscribesAndCleanupRemovesImpactObjects()
    {
        effect.Dispose();
        var view = new SimulationView(parent.transform, model, true);
        Shot(0, new Vector2(1, 1)); model.Tick(1f / 120);
        Assert.IsNotNull(parent.transform.Find("Generated Preview/Grid Impact VFX/Impact Bubble"));
        view.AdvanceEffects(config.gridImpact.duration + .01f);
        Assert.IsFalse(parent.transform.Find("Generated Preview/Grid Impact VFX/Impact Bubble").gameObject.activeSelf);
        view.Dispose(); Assert.AreEqual(0, parent.transform.childCount);
    }
}
