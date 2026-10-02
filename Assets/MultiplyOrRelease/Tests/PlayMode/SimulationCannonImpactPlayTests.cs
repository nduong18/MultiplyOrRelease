using System.Collections;
using System.Linq;
using MultiplyOrRelease;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class SimulationCannonImpactPlayTests
{
    SimulationController controller;
    SimulationConfig config;

    [TearDown] public void Cleanup()
    {
        if (controller != null) controller.enabled = false;
        if (config != null) Object.Destroy(config);
    }

    [UnityTest] public IEnumerator LethalExplosionPausesStepsFinishesAtSpeedAndRestartsCleanly()
    {
        SceneManager.LoadScene("MultiplyOrRelease");
        for (int i = 0; i < 8; i++) yield return null;
        controller = Object.FindFirstObjectByType<SimulationController>();
        config = Object.Instantiate(controller.config);
        config.autoStart = false;
        config.celebration.enableStartCountdown = false;
        config.boosts.enabled = false;
        config.cannon.destroyOnEnemyHit = true;
        config.cannonImpact = new CannonImpactSettings();
        controller.config = config; controller.Rebuild(); controller.Step();
        foreach (var team in controller.Model.teams) foreach (var ball in team.balls) ball.delay = 9999;
        controller.Model.shots.Add(new ShotState { id = 999, team = 0, position = controller.Model.teams[1].cannonPosition });
        controller.Step(); yield return null;
        Assert.IsFalse(controller.Model.teams[1].alive);
        var spark = controller.GetComponentsInChildren<SpriteRenderer>().First(r => r.name == "Spark");
        Vector3 start = spark.transform.position;
        float opacity = spark.color.a;
        yield return new WaitForSecondsRealtime(.15f);
        Assert.AreEqual(start, spark.transform.position);
        Assert.AreEqual(opacity, spark.color.a);
        controller.Step(); yield return null;
        Assert.AreNotEqual(start, spark.transform.position);
        controller.SetSpeed(8); controller.TogglePause();
        yield return new WaitForSecondsRealtime(.2f);
        Assert.IsFalse(spark.gameObject.activeInHierarchy);
        controller.Step();
        controller.Model.shots.Add(new ShotState { id = 1000, team = 0, position = controller.Model.teams[2].cannonPosition });
        controller.Step(); yield return null;
        Assert.IsTrue(controller.GetComponentsInChildren<SpriteRenderer>().Any(r => r.name == "Explosion Flash"));
        controller.RestartSameSeed(); yield return null;
        Assert.AreEqual(0, controller.GetComponentsInChildren<SpriteRenderer>(true).Count(r => r.name == "Explosion Flash"));
        Assert.IsTrue(controller.Model.teams.All(t => t.alive));
    }
}
