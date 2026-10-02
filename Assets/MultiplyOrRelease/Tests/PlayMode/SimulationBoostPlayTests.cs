using System.Collections;
using System.Linq;
using MultiplyOrRelease;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class SimulationBoostPlayTests
{
    SimulationController controller;
    SimulationConfig config;

    [TearDown] public void Cleanup()
    {
        if (controller != null) controller.enabled = false;
        if (config != null) Object.Destroy(config);
    }

    [UnityTest] public IEnumerator PickupsAndTemporaryMarblesPauseExpireAndRestartCleanly()
    {
        SceneManager.LoadScene("MultiplyOrRelease");
        for (int i = 0; i < 8; i++) yield return null;
        controller = Object.FindFirstObjectByType<SimulationController>();
        config = Object.Instantiate(controller.config);
        config.autoStart = false;
        config.celebration.enableStartCountdown = false;
        config.cannon.firingMode = FiringMode.FramesBetweenShots;
        config.boosts = new BoostSettings { firstSpawnDelay = 0, spawnIntervalMin = .1f,
            spawnIntervalMax = .1f, pickupLifetime = .2f, extraMarbleDuration = .1f };
        controller.config = config; controller.Rebuild();
        foreach (var team in controller.Model.teams) foreach (var ball in team.balls) ball.delay = 9999;
        controller.Step();
        Assert.AreEqual(1, controller.Model.boosts.Count);
        var pickup = controller.Model.boosts[0];
        Vector2 point = pickup.position;
        float expires = pickup.expiresAt;
        float elapsed = controller.Model.elapsed;
        Assert.AreEqual(1, BoostRenderers().Length);
        yield return new WaitForSecondsRealtime(.1f);
        Assert.AreEqual(elapsed, controller.Model.elapsed);
        Assert.AreEqual(point, pickup.position);
        Assert.AreEqual(expires, pickup.expiresAt);

        controller.Model.boosts.Clear();
        int permanent = controller.Model.teams[0].balls.Length;
        var position = new Vector2(-1, 1);
        Assert.IsNotNull(controller.Model.SpawnBoost(BoostKind.ExtraMarble, position));
        controller.Model.shots.Add(new ShotState { id = 999, team = 0, position = position });
        controller.Step();
        Assert.AreEqual(permanent + 1, controller.Model.teams[0].balls.Length);
        Assert.AreEqual(permanent + 1, MarbleCount(0));
        Assert.AreEqual(0, BoostRenderers().Length);
        float marbleExpiry = controller.Model.teams[0].balls.Last().expiresAt;
        yield return new WaitForSecondsRealtime(.15f);
        Assert.AreEqual(marbleExpiry, controller.Model.teams[0].balls.Last().expiresAt);
        Assert.AreEqual(permanent + 1, MarbleCount(0));
        controller.Model.teams[0].queued = 1000;
        for (int i = 0; i < 15; i++) controller.Step();
        yield return null;
        Assert.AreEqual(permanent, controller.Model.teams[0].balls.Length);
        Assert.AreEqual(permanent, MarbleCount(0));

        controller.RestartSameSeed();
        yield return null;
        Assert.AreEqual(MatchPhase.Ready, controller.Model.phase);
        Assert.AreEqual(0, controller.Model.boosts.Count);
        Assert.AreEqual(0, BoostRenderers().Length);
        Assert.AreEqual(permanent, MarbleCount(0));
        Assert.AreEqual(0, controller.Model.teams[0].fireRateBoostUntil);
        controller.Step();
        Assert.AreEqual(point, controller.Model.boosts[0].position, "Restart must replay the pickup spawn.");
    }

    SpriteRenderer[] BoostRenderers() => controller.GetComponentsInChildren<SpriteRenderer>()
        .Where(r => r.transform.parent.name.StartsWith("Boost ")).ToArray();
    int MarbleCount(int team) => controller.GetComponentsInChildren<SpriteRenderer>()
        .Count(r => r.transform.parent.name.StartsWith("Plinko Marble ") &&
            r.transform.parent.parent.name == config.teams[team].name + " Plinko Panel");
}
