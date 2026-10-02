using System.Collections;
using MultiplyOrRelease;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class SimulationControlsTests
{
    SimulationConfig frameTestConfig;
    [TearDown] public void CleanupFrameConfig()
    {
        if (frameTestConfig != null) Object.Destroy(frameTestConfig);
    }
    [UnityTest] public IEnumerator FrameFiringOncePerFrameAtHighSimulationSpeed()
    {
        yield return VerifyFrameFiring(1, 8);
    }
    [UnityTest] public IEnumerator ConfigSpeedAppliesLivePreservesMatchAndAllowsPlaybackOverrides()
    {
        SceneManager.LoadScene("MultiplyOrRelease");
        for (int frame = 0; frame < 8; frame++) yield return null;
        var controller = Object.FindFirstObjectByType<SimulationController>();
        frameTestConfig = Object.Instantiate(controller.config);
        frameTestConfig.autoStart = false;
        frameTestConfig.celebration.enableStartCountdown = false;
        frameTestConfig.boosts.enabled = false;
        frameTestConfig.simulationSpeed = 1;
        controller.config = frameTestConfig; controller.Rebuild(); controller.Step();
        var model = controller.Model;
        foreach (var team in model.teams) foreach (var ball in team.balls) ball.delay = 9999;
        model.teams[0].ammo = 37;
        float frozen = model.elapsed;
        int seed = controller.CurrentSeed;

        frameTestConfig.simulationSpeed = 4;
        yield return null;
        Assert.AreEqual(4, controller.Speed);
        Assert.AreSame(model, controller.Model);
        Assert.AreEqual(seed, controller.CurrentSeed);
        Assert.AreEqual(37, model.teams[0].ammo);
        Assert.IsTrue(controller.Paused);
        Assert.AreEqual(frozen, model.elapsed);
        controller.SetSpeed(2);
        for (int frame = 0; frame < 3; frame++) yield return null;
        Assert.AreEqual(2, controller.Speed, "Unchanged config must not overwrite Playback Speed.");
        Assert.AreEqual(4, frameTestConfig.simulationSpeed);

        frameTestConfig.simulationSpeed = .5f;
        yield return null;
        Assert.AreEqual(.5f, controller.Speed);
        frameTestConfig.simulationSpeed = 100;
        yield return null;
        Assert.AreEqual(8, controller.Speed);
        frameTestConfig.simulationSpeed = -1;
        yield return null;
        Assert.AreEqual(.1f, controller.Speed);

        controller.TogglePause();
        frameTestConfig.simulationSpeed = 2;
        float expectedAdvance = 0;
        for (int frame = 0; frame < 8; frame++)
        {
            yield return null;
            expectedAdvance += Mathf.Min(Time.unscaledDeltaTime, .25f) * 2;
            Assert.AreEqual(2, controller.Speed);
        }
        controller.TogglePause();
        Assert.AreSame(model, controller.Model);
        Assert.AreEqual(expectedAdvance, model.elapsed - frozen, 2f / frameTestConfig.ticksPerSecond);
        Assert.AreEqual(37, model.teams[0].ammo);
    }
    [UnityTest] public IEnumerator EliminatedCannonsLeaveNoRimOrMarbleAndRestartRestoresThem()
    {
        SceneManager.LoadScene("MultiplyOrRelease");
        for (int frame = 0; frame < 8; frame++) yield return null;
        var controller = Object.FindFirstObjectByType<SimulationController>();
        controller.Model.Eliminate(0); controller.Model.Eliminate(2); controller.Model.Eliminate(3);
        controller.Step();
        var visuals = controller.transform.Find("Simulation Visuals");
        for (int team = 0; team < 4; team++)
        {
            var cannon = visuals.Find(controller.config.teams[team].name + " Cannon");
            Assert.IsNotNull(cannon); Assert.AreEqual(team == 1, cannon.gameObject.activeInHierarchy);
            foreach (var graphic in cannon.GetComponentsInChildren<SpriteRenderer>(true))
                Assert.AreEqual(team == 1, graphic.gameObject.activeInHierarchy, "No rim or marble may remain visible after elimination.");
        }
        controller.RestartSameSeed();
        // Runtime Destroy removes the old visuals at the end of the frame.
        yield return null;
        visuals = controller.transform.Find("Simulation Visuals");
        for (int team = 0; team < 4; team++)
            Assert.IsTrue(visuals.Find(controller.config.teams[team].name + " Cannon").gameObject.activeInHierarchy);
    }
    [UnityTest] public IEnumerator ReleaseCountdownRendersAndPlinkoResumesAfterQueueDrains()
    {
        SceneManager.LoadScene("MultiplyOrRelease");
        for (int frame = 0; frame < 8; frame++) yield return null;
        var controller = Object.FindFirstObjectByType<SimulationController>();
        frameTestConfig = Object.Instantiate(controller.config);
        frameTestConfig.cannon.firingMode = FiringMode.FramesBetweenShots;
        frameTestConfig.cannon.framesBetweenShots = 1;
        frameTestConfig.plinko.pauseWhileReleasing = true;
        frameTestConfig.presentation.showReleaseCountdown = true;
        frameTestConfig.celebration.enableStartCountdown = false;
        frameTestConfig.cannon.destroyOnEnemyHit = false;
        controller.config = frameTestConfig; controller.Rebuild(); controller.TogglePause();
        var model = controller.Model;
        foreach (var team in model.teams) foreach (var b in team.balls) b.delay = 999;
        var ball = model.teams[0].balls[0]; ball.active = true;
        ball.position = new Vector2(0, 1); ball.velocity = Vector2.down;
        model.teams[0].ammo = 4; model.Release(0);
        yield return null;
        TextMesh ammo = null;
        foreach (var text in controller.GetComponentsInChildren<TextMesh>(true))
            if (text.name == "Stored Ammo") { ammo = text; break; }
        Assert.IsNotNull(ammo); Assert.AreEqual("4", ammo.text);
        float initialAngle = model.teams[0].angle;
        for (int shot = 1; shot <= 4; shot++)
        {
            controller.Step();
            Assert.AreEqual(Mathf.Max(1, 4 - shot).ToString(), ammo.text);
            Assert.AreEqual(new Vector2(0, 1), ball.position);
            Assert.AreEqual(Vector2.down, ball.velocity);
            yield return null;
        }
        Assert.AreNotEqual(initialAngle, model.teams[0].angle, "The barrel keeps rotating while Plinko is frozen.");
        Assert.AreEqual(4, model.teams[0].fired); Assert.AreEqual(0, model.teams[0].queued);
        Assert.Greater(model.shots.Count, 0);
        controller.Step(); Assert.AreNotEqual(new Vector2(0, 1), ball.position);
    }
    [UnityTest] public IEnumerator FrameFiringEveryTwoFramesAtLowSimulationSpeed()
    {
        yield return VerifyFrameFiring(2, .1f);
    }
    IEnumerator VerifyFrameFiring(int interval, float speed)
    {
        SceneManager.LoadScene("MultiplyOrRelease");
        for (int frame = 0; frame < 8; frame++) yield return null;
        var controller = Object.FindFirstObjectByType<SimulationController>();
        frameTestConfig = Object.Instantiate(controller.config);
        frameTestConfig.cannon.firingMode = FiringMode.FramesBetweenShots;
        frameTestConfig.cannon.framesBetweenShots = interval;
        frameTestConfig.cannon.destroyOnEnemyHit = false;
        frameTestConfig.matchTimeLimit = 0;
        frameTestConfig.celebration.enableStartCountdown = false;
        controller.config = frameTestConfig; controller.Rebuild(); controller.SetSpeed(speed);
        foreach (var team in controller.Model.teams)
        {
            team.queued = 100;
            foreach (var ball in team.balls) ball.delay = 999;
        }
        for (int frame = 0; frame < 12; frame++)
        {
            yield return null;
            foreach (var team in controller.Model.teams)
                Assert.AreEqual(1 + frame / interval, team.fired, "Unexpected shot count at render frame " + frame);
        }
        controller.TogglePause(); long frozen = controller.Model.totalFired;
        yield return new WaitForSecondsRealtime(.1f);
        Assert.AreEqual(frozen, controller.Model.totalFired, "Pause must freeze frame firing.");
        controller.Step(); Assert.AreEqual(frozen + 4, controller.Model.totalFired, "Step counts as one active firing frame.");
        yield return null;
        Assert.AreEqual(frozen + 4, controller.Model.totalFired);
    }
    [UnityTest]
    public IEnumerator LoadedSceneUsesInspectorControlsWithoutScreenUi()
    {
        SceneManager.LoadScene("MultiplyOrRelease");
        for (int frame = 0; frame < 8; frame++) yield return null;
        var controller = Object.FindFirstObjectByType<SimulationController>();
        Assert.IsNotNull(controller); Assert.IsNotNull(controller.Model);
        Assert.AreEqual(0, Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Length);
        Assert.IsNull(controller.transform.Find("Simulation HUD"));
        float aspect = controller.simulationCamera.aspect;
        float visibleWidth = controller.simulationCamera.orthographicSize * 2 * aspect;
        float boardWidth = controller.Model.config.SimulationSize.x;
        Assert.GreaterOrEqual(visibleWidth + .0001f, boardWidth);
        Assert.Less(visibleWidth, boardWidth * 1.25f, "Camera should closely frame the board in the default landscape Game view.");
        controller.TogglePause();
        Assert.IsTrue(controller.Paused);
        float frozen = controller.Model.elapsed;
        yield return new WaitForSecondsRealtime(.1f);
        Assert.AreEqual(frozen, controller.Model.elapsed);
        controller.Step();
        Assert.AreEqual(1f / controller.config.ticksPerSecond, controller.Model.elapsed - frozen, .0001f);
        controller.SetGridStyle(TerritoryStyle.Flag);
        Assert.AreEqual(TerritoryStyle.Flag, controller.GridStyle);
        controller.SetSpeed(2);
        Assert.AreEqual(2, controller.Speed);
        controller.SetTrailsVisible(false);
        Assert.IsFalse(controller.TrailsVisible);
    }
    [UnityTest]
    public IEnumerator PanelsShareRectangleEdgesAndCameraFitsActualRenderBounds()
    {
        SceneManager.LoadScene("MultiplyOrRelease");
        for (int frame = 0; frame < 8; frame++) yield return null;
        var controller = Object.FindFirstObjectByType<SimulationController>();
        var renderers = controller.GetComponentsInChildren<SpriteRenderer>();
        SpriteRenderer arena = null, outer = null;
        var panels = new System.Collections.Generic.List<SpriteRenderer>();
        foreach (var renderer in renderers)
        {
            string name = renderer.transform.parent.name;
            if (name == "Arena Frame") arena = renderer;
            else if (name == "Simulation Frame") outer = renderer;
            else if (name.EndsWith("Plinko Frame")) panels.Add(renderer);
        }
        Assert.IsNotNull(arena); Assert.IsNotNull(outer); Assert.AreEqual(4, panels.Count);
        Assert.AreEqual(16f / 9f, outer.bounds.size.x / outer.bounds.size.y, .00001f);
        foreach (var panel in panels)
        {
            if (panel.bounds.center.y > 0) Assert.AreEqual(arena.bounds.max.y, panel.bounds.max.y, .00001f);
            else Assert.AreEqual(arena.bounds.min.y, panel.bounds.min.y, .00001f);
        }
        var camera = controller.simulationCamera;
        Vector3 min = camera.WorldToViewportPoint(outer.bounds.min);
        Vector3 max = camera.WorldToViewportPoint(outer.bounds.max);
        Assert.GreaterOrEqual(min.x, -.00001f); Assert.GreaterOrEqual(min.y, -.00001f);
        Assert.LessOrEqual(max.x, 1.00001f); Assert.LessOrEqual(max.y, 1.00001f);
        if (Mathf.Abs(camera.aspect - 16f / 9f) < .00001f)
        {
            Assert.AreEqual(0, min.x, .00001f); Assert.AreEqual(0, min.y, .00001f);
            Assert.AreEqual(1, max.x, .00001f); Assert.AreEqual(1, max.y, .00001f);
        }
    }
}
