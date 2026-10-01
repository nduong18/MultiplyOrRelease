using System.Collections;
using MultiplyOrRelease;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class SimulationTeamLineupPlayTests
{
    SimulationController controller;
    SimulationConfig config;
    TeamLineupPreset preset;

    [TearDown] public void Cleanup()
    {
        if (controller != null) controller.enabled = false;
        if (config != null) Object.Destroy(config);
        if (preset != null) Object.Destroy(preset);
    }

    [UnityTest] public IEnumerator EditingLineupDoesNotChangeRunningSessionUntilRestartAndPresetCanRestoreIt()
    {
        SceneManager.LoadScene("MultiplyOrRelease");
        for (int i = 0; i < 8; i++) yield return null;
        controller = Object.FindFirstObjectByType<SimulationController>();
        config = Object.Instantiate(controller.config);
        config.autoStart = false; config.celebration.enableStartCountdown = false;
        controller.config = config; controller.Rebuild();
        preset = ScriptableObject.CreateInstance<TeamLineupPreset>(); preset.CaptureFrom(config);
        string name = config.teams[0].name;
        TeamSetup.Move(config, 0, 3);
        Assert.AreEqual(name, controller.Model.config.teams[0].name);
        Assert.AreEqual(name, config.teams[3].name);
        controller.RestartSameSeed();
        yield return null;
        Assert.AreEqual(name, controller.Model.config.teams[3].name);
        Assert.Greater(controller.Model.teams[3].cannonPosition.x, 0);
        Assert.Less(controller.Model.teams[3].cannonPosition.y, 0);
        var panel = controller.transform.Find("Simulation Visuals/" + name + " Plinko Panel");
        Assert.IsNotNull(panel);
        Assert.Greater(panel.GetComponentInChildren<SpriteRenderer>().bounds.center.x, 0);
        Assert.Less(panel.GetComponentInChildren<SpriteRenderer>().bounds.center.y, 0);
        preset.ApplyTo(config); controller.RestartSameSeed();
        yield return null;
        Assert.AreEqual(name, controller.Model.config.teams[0].name);
        Assert.Less(controller.Model.teams[0].cannonPosition.x, 0);
        Assert.Greater(controller.Model.teams[0].cannonPosition.y, 0);
    }
}
