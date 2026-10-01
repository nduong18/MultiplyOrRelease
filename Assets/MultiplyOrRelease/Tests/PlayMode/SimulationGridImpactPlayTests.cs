using System.Collections;
using System.Linq;
using MultiplyOrRelease;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class SimulationGridImpactPlayTests
{
    SimulationController controller;
    SimulationConfig config;

    [TearDown] public void Cleanup()
    {
        if (controller != null) controller.enabled = false;
        if (config != null) Object.Destroy(config);
    }

    [UnityTest] public IEnumerator OneCirclePerHitPausesStepsFadesAndRestartsCleanly()
    {
        SceneManager.LoadScene("MultiplyOrRelease");
        for (int i = 0; i < 8; i++) yield return null;
        controller = Object.FindFirstObjectByType<SimulationController>();
        config = Object.Instantiate(controller.config);
        config.autoStart = false;
        config.celebration.enableStartCountdown = false;
        config.gridImpact.enabled = true;
        controller.config = config; controller.Rebuild();
        foreach (var team in controller.Model.teams) foreach (var ball in team.balls) ball.delay = 999;
        controller.Model.shots.Add(new ShotState { id = 99, team = 0, position = new Vector2(1, 1) });
        controller.Step();
        yield return null;
        var circles = controller.GetComponentsInChildren<SpriteRenderer>().Where(r => r.name == "Circle").ToArray();
        Assert.AreEqual(1, circles.Length);
        Assert.AreEqual(0, controller.Model.shots.Count);
        Vector3 position = circles[0].transform.parent.localPosition;
        float alpha = circles[0].color.a;
        yield return new WaitForSecondsRealtime(.1f);
        Assert.AreEqual(position, circles[0].transform.parent.localPosition);
        Assert.AreEqual(alpha, circles[0].color.a);
        controller.Step();
        Assert.AreNotEqual(position, circles[0].transform.parent.localPosition);
        controller.TogglePause();
        yield return new WaitForSecondsRealtime(config.gridImpact.duration + .2f);
        Assert.IsFalse(circles[0].gameObject.activeInHierarchy);
        controller.RestartSameSeed();
        yield return null;
        Assert.AreEqual(0, controller.GetComponentsInChildren<SpriteRenderer>(true).Count(r => r.name == "Circle"));
    }
}
