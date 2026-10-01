using MultiplyOrRelease;
using NUnit.Framework;
using UnityEngine;

public class SimulationCannonPopTests
{
    SimulationConfig config;
    SimulationModel model;
    SimulationView view;
    GameObject parent;

    [SetUp] public void Setup()
    {
        config = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<SimulationConfig>(
            "Assets/MultiplyOrRelease/Config/DefaultSimulation.asset"));
        config.cannon.enableFirePop = true;
        config.cannon.firePopScale = 1.2f; config.cannon.firePopDuration = .12f;
        config.cannon.firingMode = FiringMode.FramesBetweenShots;
        config.cannon.framesBetweenShots = 1;
        model = new SimulationModel(config, 1207); model.Start();
        foreach (var team in model.teams) foreach (var ball in team.balls) ball.delay = 999;
        parent = new GameObject("Cannon Pop Test");
        view = new SimulationView(parent.transform, model, true);
    }

    [TearDown] public void Cleanup()
    {
        view?.Dispose(); Object.DestroyImmediate(parent); Object.DestroyImmediate(config);
    }

    Transform Cannon(int team) => parent.transform.Find("Generated Preview/" + config.teams[team].name + " Cannon");
    Transform Flag(int team) => Cannon(team).Find("Cannon Marble");
    void Fire(int team) { model.teams[team].queued++; model.AdvanceFiringFrame(); view.Render(); }

    [TestCase(FiringMode.ShotsPerSecond)] [TestCase(FiringMode.FramesBetweenShots)]
    public void RealShotPopsOnlyFiringFlagInBothModesAndPreservesItsCentreAndPhysics(FiringMode mode)
    {
        config.cannon.firingMode = mode;
        var graphic = Flag(1).GetComponentInChildren<SpriteRenderer>();
        Vector3 centre = graphic.bounds.center;
        float diameter = graphic.bounds.size.x, hitRadius = config.cannon.hitRadius;
        Vector2 cannonPosition = model.teams[1].cannonPosition;
        int events = 0;
        model.ShotFired += team => { Assert.AreEqual(1, team); Assert.Greater(model.teams[team].fired, 0); events++; };
        model.teams[1].queued = 1;
        if (mode == FiringMode.ShotsPerSecond) model.Tick(.1f); else model.AdvanceFiringFrame();
        view.Render();
        Assert.AreEqual(1, events); Assert.AreEqual(1, model.teams[1].fired);
        Assert.AreEqual(1.2f, Flag(1).localScale.x, .0001f);
        Assert.AreEqual(diameter * 1.2f, graphic.bounds.size.x, .0001f);
        Assert.AreEqual(centre.x, graphic.bounds.center.x, .0001f);
        Assert.AreEqual(centre.y, graphic.bounds.center.y, .0001f);
        Assert.AreEqual(hitRadius, config.cannon.hitRadius); Assert.AreEqual(cannonPosition, model.teams[1].cannonPosition);
        Assert.AreEqual(Vector3.one, Cannon(1).localScale);
        Assert.AreEqual(Vector3.one, Cannon(1).Find("Sweep Pivot").localScale);
        for (int team = 0; team < 4; team++) if (team != 1) Assert.AreEqual(Vector3.one, Flag(team).localScale);
    }

    [Test] public void PopEasesBackAndZeroDeltaAndRenderingDoNotAdvanceIt()
    {
        Fire(0);
        view.AdvanceEffects(0); view.Render(); view.Render();
        Assert.AreEqual(1.2f, Flag(0).localScale.x, .0001f);
        view.AdvanceEffects(.06f);
        Assert.AreEqual(1.025f, Flag(0).localScale.x, .0001f);
        view.AdvanceEffects(.06f);
        Assert.AreEqual(Vector3.one, Flag(0).localScale);
    }

    [Test] public void RapidFireDoesNotStackOrExtendTheActivePulseAndCanPopAgain()
    {
        Fire(0); view.AdvanceEffects(.06f);
        for (int i = 0; i < 30; i++) Fire(0);
        Assert.AreEqual(31, model.teams[0].fired);
        Assert.AreEqual(1.025f, Flag(0).localScale.x, .0001f);
        view.AdvanceEffects(.06f);
        Assert.AreEqual(Vector3.one, Flag(0).localScale);
        Fire(0); Assert.AreEqual(1.2f, Flag(0).localScale.x, .0001f);
    }

    [Test] public void QueuedShotsAtActiveCapacityDoNotPopUntilActuallyFired()
    {
        for (int i = 0; i < config.projectile.maxActive; i++) model.shots.Add(new ShotState());
        model.teams[0].queued = 1; model.AdvanceFiringFrame();
        Assert.AreEqual(0, model.teams[0].fired); Assert.AreEqual(Vector3.one, Flag(0).localScale);
        model.shots.Clear(); model.AdvanceFiringFrame();
        Assert.AreEqual(1, model.teams[0].fired); Assert.AreEqual(1.2f, Flag(0).localScale.x, .0001f);
    }

    [Test] public void DisablingOrEliminatingResetsScaleAndViewDisposalUnsubscribes()
    {
        config.cannon.enableFirePop = false; Fire(0);
        Assert.AreEqual(Vector3.one, Flag(0).localScale);
        config.cannon.enableFirePop = true; Fire(0);
        config.cannon.enableFirePop = false; view.Render();
        Assert.AreEqual(Vector3.one, Flag(0).localScale);
        config.cannon.enableFirePop = true; Fire(1);
        model.Eliminate(1); view.Render();
        Assert.AreEqual(Vector3.one, Flag(1).localScale); Assert.IsFalse(Cannon(1).gameObject.activeSelf);
        view.Dispose(); view = null;
        model.teams[0].queued = 1;
        Assert.DoesNotThrow(() => model.AdvanceFiringFrame());
    }
}
