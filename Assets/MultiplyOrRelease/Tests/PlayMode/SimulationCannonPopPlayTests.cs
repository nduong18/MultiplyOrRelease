using System.Collections;
using MultiplyOrRelease;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class SimulationCannonPopPlayTests
{
    SimulationController controller;
    SimulationConfig config;

    [TearDown] public void Cleanup()
    {
        if (controller != null) controller.enabled = false;
        if (config != null) Object.Destroy(config);
    }

    [UnityTest] public IEnumerator StepTriggersPopPauseHoldsResumeSettlesAndRestartResets()
    {
        SceneManager.LoadScene("MultiplyOrRelease");
        for (int i = 0; i < 8; i++) yield return null;
        controller = Object.FindFirstObjectByType<SimulationController>();
        config = Object.Instantiate(controller.config);
        config.autoStart = false; config.celebration.enableStartCountdown = false;
        config.cannon.firingMode = FiringMode.FramesBetweenShots;
        config.cannon.enableFirePop = true;
        config.cannon.firePopScale = 1.2f; config.cannon.firePopDuration = .12f;
        controller.config = config; controller.Rebuild();
        yield return null; // Rebuild's old visual root is destroyed at the end of the frame.
        foreach (var team in controller.Model.teams) foreach (var ball in team.balls) ball.delay = 999;
        controller.Model.teams[0].queued = 1; controller.Step();
        var flag = controller.transform.Find("Simulation Visuals/" + config.teams[0].name + " Cannon/Cannon Marble");
        Assert.AreEqual(1.2f, flag.localScale.x, .0001f);
        yield return new WaitForSecondsRealtime(.2f);
        Assert.AreEqual(1.2f, flag.localScale.x, .0001f);
        controller.Step(); Assert.Less(flag.localScale.x, 1.2f); Assert.Greater(flag.localScale.x, 1);
        controller.TogglePause();
        yield return new WaitForSecondsRealtime(.3f);
        Assert.AreEqual(Vector3.one, flag.localScale);
        controller.RestartSameSeed();
        yield return null;
        var reset = controller.transform.Find("Simulation Visuals/" + config.teams[0].name + " Cannon/Cannon Marble");
        Assert.AreEqual(Vector3.one, reset.localScale);
    }
}
