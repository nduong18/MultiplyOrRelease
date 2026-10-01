using System;
using MultiplyOrRelease;
using NUnit.Framework;
using UnityEngine;

public class SimulationProjectileSpriteTests
{
    SimulationConfig config;
    SimulationModel model;
    SimulationView view;
    GameObject parent;

    [SetUp] public void SetUp()
    {
        config = UnityEngine.Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<SimulationConfig>(
            "Assets/MultiplyOrRelease/Config/DefaultSimulation.asset"));
        config.board.columns = config.board.rows = 8;
        config.projectile.useTeamFlagSprite = true;
        config.projectile.visualScale = 1;
        config.projectile.matchTrailToSize = true;
        config.projectile.taperTrail = false;
        config.plinko.taperTrail = false;
        model = new SimulationModel(config, 1207);
        parent = new GameObject("Projectile Sprite Test");
        view = new SimulationView(parent.transform, model, true);
    }

    [TearDown] public void TearDown()
    {
        view?.Dispose();
        if (parent != null) UnityEngine.Object.DestroyImmediate(parent);
        if (config != null) UnityEngine.Object.DestroyImmediate(config);
    }

    SpriteRenderer[] Bullets() => Array.FindAll(parent.GetComponentsInChildren<SpriteRenderer>(),
        r => r.transform.parent.name == "Pooled Projectile");

    void Spawn(int id, int team, Vector2 position)
    {
        model.shots.Add(new ShotState { id = id, team = team, position = position });
        view.Render();
    }

    void Clear() { model.shots.Clear(); view.Render(); }

    void AssertGeometry(SpriteRenderer renderer, Vector2 position)
    {
        float diameter = config.projectile.radius * 2 * config.projectile.visualScale;
        Assert.AreEqual(diameter, renderer.bounds.size.x, .00001f);
        Assert.AreEqual(diameter, renderer.bounds.size.y, .00001f);
        Assert.AreEqual(position.x, renderer.bounds.center.x, .00001f);
        Assert.AreEqual(position.y, renderer.bounds.center.y, .00001f);
        var trail = renderer.transform.parent.GetComponentInChildren<TrailRenderer>();
        float width = config.projectile.matchTrailToSize ? diameter : config.projectile.trailWidth;
        Assert.AreEqual(width, trail.startWidth, .00001f);
        Assert.AreEqual(config.projectile.taperTrail ? 0 : width, trail.endWidth, .00001f);
    }

    [Test] public void EachTeamUsesItsFlagWithoutTeamColorTint()
    {
        for (int t = 0; t < 4; t++)
        {
            config.teams[t].projectileSprite = config.teams[t].cannonSprite;
            config.teams[t].projectileSpriteTint = Color.white;
            Spawn(t + 1, t, new Vector2(t - 2, 1));
        }
        var bullets = Bullets();
        Assert.AreEqual(4, bullets.Length);
        for (int t = 0; t < 4; t++)
        {
            var r = Array.Find(bullets, b => Mathf.Abs(b.transform.parent.localPosition.x - (t - 2)) < .001f);
            Assert.AreSame(config.teams[t].projectileSprite, r.sprite);
            Assert.AreEqual(Color.white, r.color);
            AssertGeometry(r, new Vector2(t - 2, 1));
        }
    }

    [Test] public void ReusedPoolEntryChangesSpriteSizePivotTintAndTrailForNewTeam()
    {
        config.teams[0].projectileSprite = config.presentation.squareSprite;
        config.teams[1].projectileSprite = config.teams[1].cannonSprite;
        config.teams[1].projectileSpriteTint = new Color(.8f, .9f, 1);
        Spawn(1, 0, Vector2.zero);
        var previous = Bullets()[0];
        Clear();
        config.projectile.visualScale = 2;
        Spawn(2, 1, new Vector2(2, -1));
        var current = Bullets()[0];
        Assert.AreSame(previous, current);
        Assert.AreSame(config.teams[1].projectileSprite, current.sprite);
        Assert.AreEqual(config.teams[1].projectileSpriteTint, current.color);
        // Trail gradients quantize colors to 8-bit channels.
        var trailColor = current.transform.parent.GetComponentInChildren<TrailRenderer>().startColor;
        var expectedTrail = config.teams[1].projectileTrailColor;
        for (int channel = 0; channel < 4; channel++)
            Assert.AreEqual(expectedTrail[channel], trailColor[channel], 1f / 255);
        AssertGeometry(current, new Vector2(2, -1));
    }

    [Test] public void ToggleOffRestoresColoredCircleEvenWhenPoolPreviouslyUsedFlag()
    {
        Spawn(1, 1, Vector2.zero);
        Clear();
        config.projectile.useTeamFlagSprite = false;
        Spawn(2, 1, new Vector2(1, 2));
        var r = Bullets()[0];
        Assert.AreSame(config.presentation.circleSprite, r.sprite);
        Assert.AreEqual(config.teams[1].projectileColor, r.color);
        AssertGeometry(r, new Vector2(1, 2));
    }

    [Test] public void MissingProjectileSpriteFallsBackToCannonThenColoredCircle()
    {
        config.teams[1].projectileSprite = null;
        Spawn(1, 1, Vector2.zero);
        Assert.AreSame(config.teams[1].cannonSprite, Bullets()[0].sprite);
        Clear();
        config.teams[1].cannonSprite = null;
        Spawn(2, 1, Vector2.zero);
        Assert.AreSame(config.presentation.circleSprite, Bullets()[0].sprite);
        Assert.AreEqual(config.teams[1].projectileColor, Bullets()[0].color);
        AssertGeometry(Bullets()[0], Vector2.zero);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void DoubleVisualScaleDoublesBulletSizeWithoutChangingCollisionRadius(bool flag)
    {
        float radius = config.projectile.radius;
        config.projectile.useTeamFlagSprite = flag;
        config.projectile.visualScale = 2;
        Spawn(1, 1, new Vector2(1, 2));
        Assert.AreEqual(radius * 4, Bullets()[0].bounds.size.x, .00001f);
        AssertGeometry(Bullets()[0], new Vector2(1, 2));
        Assert.AreEqual(radius, model.config.projectile.radius);
    }

    [Test] public void CustomWidthAndLegacyTaperAreReappliedOnProjectilePoolReuse()
    {
        Spawn(1, 0, Vector2.zero);
        var previous = Bullets()[0];
        Clear();
        config.projectile.matchTrailToSize = false;
        config.projectile.trailWidth = .3f;
        config.projectile.taperTrail = true;
        Spawn(2, 1, new Vector2(1, 2));
        Assert.AreSame(previous, Bullets()[0]);
        AssertGeometry(Bullets()[0], new Vector2(1, 2));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void PlinkoTailWidthFollowsTaperOptionForEveryMarble(bool taper)
    {
        config.plinko.taperTrail = taper;
        view.Dispose();
        view = new SimulationView(parent.transform, model, true);
        var trails = Array.FindAll(parent.GetComponentsInChildren<TrailRenderer>(true),
            t => t.transform.parent.name.StartsWith("Plinko Marble"));
        Assert.AreEqual(config.plinko.ballCount * 4, trails.Length);
        foreach (var trail in trails)
        {
            Assert.AreEqual(config.plinko.trailWidth, trail.startWidth, .00001f);
            Assert.AreEqual(taper ? 0 : config.plinko.trailWidth, trail.endWidth, .00001f);
            if (!taper)
                for (int step = 0; step <= 4; step++)
                    Assert.AreEqual(trail.widthCurve.Evaluate(0), trail.widthCurve.Evaluate(step / 4f), .00001f);
        }
    }
}
