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
            spawnIntervalMax = .1f, pickupLifetime = .2f, extraMarbleDuration = .1f, animateCollection = false };
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

    [UnityTest] public IEnumerator CollectedBoostFlightPausesStepsScalesWithSpeedAndRestartsCleanly()
    {
        SceneManager.LoadScene("MultiplyOrRelease");
        for (int i = 0; i < 8; i++) yield return null;
        controller = Object.FindFirstObjectByType<SimulationController>();
        config = Object.Instantiate(controller.config);
        config.autoStart = false;
        config.celebration.enableStartCountdown = false;
        config.boosts = new BoostSettings { firstSpawnDelay = 9999, collectionFlightDuration = 1.2f };
        controller.config = config; controller.Rebuild(); controller.Step();
        foreach (var team in controller.Model.teams) foreach (var ball in team.balls) ball.delay = 9999;
        var point = new Vector2(-1, 1);
        long ammo = controller.Model.teams[0].ammo;
        Assert.IsNotNull(controller.Model.SpawnBoost(BoostKind.DoubleAmmo, point));
        controller.Model.shots.Add(new ShotState { id = 999, team = 0, position = point });
        controller.Step(); yield return null;
        var flight = controller.GetComponentsInChildren<SpriteRenderer>()
            .Single(r => r.transform.parent.name == "Collected Boost DoubleAmmo");
        var pivot = flight.transform.parent;
        Vector3 start = pivot.localPosition;
        Assert.AreEqual((Vector3)point, start);
        Assert.AreEqual(ammo, controller.Model.teams[0].ammo);
        yield return new WaitForSecondsRealtime(.15f);
        Assert.AreEqual(start, pivot.localPosition);
        Assert.AreEqual(ammo, controller.Model.teams[0].ammo);
        controller.Step(); yield return null;
        Assert.AreNotEqual(start, pivot.localPosition);
        Assert.AreEqual(ammo, controller.Model.teams[0].ammo);
        Vector2 target = controller.Model.teams[0].cannonPosition;
        Assert.Less(Vector2.Distance(pivot.localPosition, target), Vector2.Distance(start, target));
        controller.SetSpeed(8); controller.TogglePause();
        yield return new WaitForSecondsRealtime(.25f);
        Assert.IsFalse(flight.gameObject.activeInHierarchy, "The 1.2 simulation-second flight finishes at Speed 8.");
        Assert.AreEqual(ammo * 2, controller.Model.teams[0].ammo);
        Assert.AreEqual(0, controller.Model.collectingBoosts.Count);
        controller.TogglePause();
        Assert.IsNotNull(controller.Model.SpawnBoost(BoostKind.FireRate, point));
        controller.Model.shots.Add(new ShotState { id = 1000, team = 0, position = point });
        controller.Step(); yield return null;
        Assert.AreEqual(1, controller.GetComponentsInChildren<SpriteRenderer>().Count(r => r.transform.parent.name.StartsWith("Collected Boost ")));
        Assert.AreEqual(1, controller.Model.FireRateScale(0));
        controller.RestartSameSeed(); yield return null;
        Assert.AreEqual(0, controller.GetComponentsInChildren<SpriteRenderer>(true).Count(r => r.transform.parent.name.StartsWith("Collected Boost ")));
        Assert.AreEqual(0, controller.Model.boosts.Count);
        Assert.AreEqual(0, controller.Model.collectingBoosts.Count);
        Assert.AreEqual(1, controller.Model.FireRateScale(0));
    }

    SpriteRenderer[] BoostRenderers() => controller.GetComponentsInChildren<SpriteRenderer>()
        .Where(r => r.transform.parent.name.StartsWith("Boost ")).ToArray();
    int MarbleCount(int team) => controller.GetComponentsInChildren<SpriteRenderer>()
        .Count(r => r.transform.parent.name.StartsWith("Plinko Marble ") &&
            r.transform.parent.parent.name == config.teams[team].name + " Plinko Panel");
}
