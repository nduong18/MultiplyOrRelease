using System.Collections;
using System.Collections.Generic;
using MultiplyOrRelease;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class SimulationCelebrationTests
{
    SimulationController controller;
    SimulationConfig config;

    IEnumerator Load(bool countdown, bool autoStart = true)
    {
        SceneManager.LoadScene("MultiplyOrRelease");
        for (int i = 0; i < 8; i++) yield return null;
        controller = Object.FindFirstObjectByType<SimulationController>();
        config = Object.Instantiate(controller.config);
        config.autoStart = autoStart;
        config.resultDelay = 0;
        config.celebration.victoryCardDelay = 0;
        config.cannon.destroyOnEnemyHit = false;
        config.celebration.enableStartCountdown = countdown;
        config.celebration.countdownStartDelay = 0;
        config.celebration.countdownStepDuration = .1f;
        controller.config = config;
        controller.Rebuild();
        yield return null;
    }

    [TearDown] public void Cleanup()
    {
        if (controller != null) controller.enabled = false;
        if (config != null) Object.Destroy(config);
    }

    Text Label(string name)
    {
        foreach (var text in controller.GetComponentsInChildren<Text>(true))
            if (text.name == name) return text;
        return null;
    }

    [UnityTest] public IEnumerator IntroShowsAllFourStepsAndHoldsSimulationUntilGoEnds()
    {
        yield return Load(true);
        controller.SetSpeed(8);
        var seen = new List<string>();
        float deadline = Time.realtimeSinceStartup + 3;
        while (controller.CountdownActive && Time.realtimeSinceStartup < deadline)
        {
            var label = Label("Countdown Text");
            Assert.IsNotNull(label);
            if (label.text.Length > 0 && (seen.Count == 0 || seen[seen.Count - 1] != label.text)) seen.Add(label.text);
            Assert.AreEqual(MatchPhase.Ready, controller.Model.phase);
            Assert.AreEqual(0, controller.Model.elapsed);
            Assert.AreEqual(0, controller.Model.totalFired);
            yield return null;
        }
        CollectionAssert.AreEqual(new[] { "3", "2", "1", "GO!" }, seen);
        Assert.IsFalse(controller.CountdownActive);
        Assert.AreEqual(MatchPhase.Running, controller.Model.phase);
        yield return null; // Runtime Destroy completes at the end of the frame.
        Assert.IsNull(Label("Countdown Text"));
    }

    [UnityTest] public IEnumerator PausingIntroFreezesTextAndStepBypassesIt()
    {
        yield return Load(true);
        controller.TogglePause();
        string value = Label("Countdown Text").text;
        yield return new WaitForSecondsRealtime(.25f);
        Assert.AreEqual(value, Label("Countdown Text").text);
        Assert.AreEqual(0, controller.Model.elapsed);
        controller.Step();
        yield return null;
        Assert.IsFalse(controller.CountdownActive);
        Assert.IsNull(Label("Countdown Text"));
        Assert.IsTrue(controller.Paused);
        Assert.Greater(controller.Model.elapsed, 0);
    }

    [UnityTest] public IEnumerator WinnerUsesCurrentTeamFlagAndConfettiAndRestartRemovesEverything()
    {
        yield return Load(false, false);
        controller.Model.Eliminate(0); controller.Model.Eliminate(2); controller.Model.Eliminate(3);
        controller.Step();
        yield return new WaitForSecondsRealtime(.1f);
        Assert.AreEqual(MatchPhase.Finished, controller.Model.phase);
        Assert.AreEqual(1, controller.Model.winner);
        Assert.AreEqual(config.teams[1].name, Label("Team Name").text);
        Assert.AreEqual("WINNER!", Label("Status").text);
        var image = controller.transform.Find("Match Presentation/Victory Card/Card/Flag").GetComponent<Image>();
        Assert.AreSame(config.teams[1].cannonSprite, image.sprite);
        int particles = 0;
        foreach (var system in controller.GetComponentsInChildren<ParticleSystem>()) particles += system.particleCount;
        Assert.Greater(particles, 0);
        for (int i = 0; i < 3; i++) yield return null;
        Assert.AreEqual(1, controller.GetComponentsInChildren<Canvas>().Length);
        Assert.AreEqual(1, controller.GetComponentsInChildren<AudioSource>().Length);
        controller.RestartSameSeed();
        yield return null;
        Assert.IsNull(controller.transform.Find("Match Presentation/Victory Card"));
        Assert.AreEqual(0, controller.GetComponentsInChildren<ParticleSystem>().Length);
        Assert.IsFalse(controller.GetComponentInChildren<AudioSource>().isPlaying);
    }

    [UnityTest] public IEnumerator FinalEnemyHitLocksWinnerImmediatelyAndShowsCardAfterOneRealSecond()
    {
        yield return Load(false, false);
        config.resultDelay = 90; // A legacy delay must not postpone the winning card.
        config.cannon.destroyOnEnemyHit = true;
        config.cannon.firingMode = FiringMode.FramesBetweenShots;
        config.boosts.enabled = false;
        config.celebration.victoryCardDelay = 1;
        controller.Rebuild(); controller.Step();
        var model = controller.Model;
        foreach (var team in model.teams) foreach (var ball in team.balls) ball.delay = 9999;
        model.Eliminate(2); model.Eliminate(3);
        model.teams[0].queued = 100;
        model.shots.Add(new ShotState { id = 999, team = 1, position = model.teams[0].cannonPosition });
        model.shots.Add(new ShotState { id = 1000, team = 0, position = model.teams[1].cannonPosition });
        controller.Step();
        Assert.AreEqual(MatchPhase.Finished, model.phase);
        Assert.AreEqual(0, model.winner);
        Assert.IsTrue(model.teams[0].alive);
        Assert.AreEqual(0, model.shots.Count);
        Assert.AreEqual(0, model.teams[0].queued);
        Assert.AreEqual(0, model.totalFired);
        Assert.IsNull(Label("Status"));
        Assert.IsNull(controller.transform.Find("Match Presentation/Victory Card"));
        Assert.IsNotNull(controller.transform.Find("Simulation Visuals/Cannon Impact VFX/Cannon Explosion"),
            "The final explosion must appear before the delayed winner card.");
        controller.SetSpeed(8);
        yield return new WaitForSecondsRealtime(.45f);
        Assert.IsNull(Label("Status"), "Simulation Speed must not shorten the card delay.");
        yield return new WaitForSecondsRealtime(.65f);
        Assert.AreEqual(config.teams[0].name, Label("Team Name").text);
        Assert.AreEqual("WINNER!", Label("Status").text);
        var card = controller.transform.Find("Match Presentation/Victory Card");
        Assert.IsNotNull(card);
        Assert.IsTrue(card.gameObject.activeInHierarchy);
        yield return null;
        Assert.AreEqual(1, controller.GetComponentsInChildren<Canvas>().Length);
        controller.RestartSameSeed(); yield return null;
        Assert.IsNull(Label("Status"));
        Assert.AreEqual(MatchPhase.Ready, controller.Model.phase);
    }

    [UnityTest] public IEnumerator RestartDuringWinnerDelayCancelsPendingCardAndWinnerSound()
    {
        yield return Load(false, false);
        config.celebration.victoryCardDelay = 1;
        controller.Rebuild();
        controller.Model.Eliminate(1); controller.Model.Eliminate(2); controller.Model.Eliminate(3);
        controller.Step();
        Assert.AreEqual(MatchPhase.Finished, controller.Model.phase);
        Assert.IsNull(Label("Status"));
        Assert.IsFalse(controller.GetComponentInChildren<AudioSource>().isPlaying);
        yield return new WaitForSecondsRealtime(.2f);
        controller.RestartSameSeed();
        yield return new WaitForSecondsRealtime(1.1f);
        Assert.AreEqual(MatchPhase.Ready, controller.Model.phase);
        Assert.IsNull(Label("Status"));
        Assert.AreEqual(0, controller.GetComponentsInChildren<ParticleSystem>().Length);
        Assert.IsFalse(controller.GetComponentInChildren<AudioSource>().isPlaying);
    }

    [UnityTest] public IEnumerator ExplosionSoundPlaysOnlyWhenEnemyBulletDestroysFlag()
    {
        yield return Load(false, false);
        Assert.IsNotNull(config.celebration.explosionClip);
        Assert.AreEqual("explosion", config.celebration.explosionClip.name);
        config.celebration.enableSounds = true;
        config.celebration.enableVictoryCard = false;
        controller.Rebuild(); controller.Step();
        var model = controller.Model;
        foreach (var team in model.teams) foreach (var ball in team.balls) ball.delay = 9999;
        var source = controller.GetComponentInChildren<AudioSource>();
        model.teams[1].health = 2;
        model.shots.Add(new ShotState { id = 999, team = 0, position = model.teams[1].cannonPosition });
        controller.Step(); yield return null;
        Assert.IsTrue(model.teams[1].alive);
        Assert.IsFalse(source.isPlaying, "A nonlethal hit must not play the destruction clip.");
        model.Eliminate(2); model.Eliminate(3);
        model.shots.Add(new ShotState { id = 1000, team = 0, position = model.teams[1].cannonPosition });
        controller.Step(); yield return null;
        Assert.AreEqual(MatchPhase.Finished, model.phase);
        Assert.IsTrue(source.isPlaying, "The final winning explosion must still be audible.");
        controller.RestartSameSeed(); yield return null;
        Assert.IsFalse(controller.GetComponentInChildren<AudioSource>().isPlaying);
    }

    [UnityTest] public IEnumerator CollectSoundWaitsForBoostArrivalAndCanceledDeliveryIsSilent()
    {
        yield return Load(false, false);
        Assert.IsNotNull(config.celebration.collectClip);
        Assert.AreEqual("collect", config.celebration.collectClip.name);
        config.celebration.enableSounds = true;
        config.boosts.firstSpawnDelay = 9999;
        config.boosts.collectionFlightDuration = .1f;
        controller.Rebuild(); controller.Step();
        var model = controller.Model;
        foreach (var team in model.teams) foreach (var ball in team.balls) ball.delay = 9999;
        var source = controller.GetComponentInChildren<AudioSource>();
        var point = new Vector2(-1, 1);
        long ammo = model.teams[0].ammo;
        Assert.IsNotNull(model.SpawnBoost(BoostKind.DoubleAmmo, point));
        model.shots.Add(new ShotState { id = 999, team = 0, position = point });
        controller.Step(); yield return null;
        Assert.AreEqual(ammo, model.teams[0].ammo);
        Assert.IsFalse(source.isPlaying, "Hitting the pickup only starts its flight.");
        model.Tick(.05f); yield return null;
        Assert.IsFalse(source.isPlaying);
        model.Tick(.06f); yield return null;
        Assert.AreEqual(ammo * 2, model.teams[0].ammo);
        Assert.IsTrue(source.isPlaying);
        source.Stop();
        Assert.IsNotNull(model.SpawnBoost(BoostKind.FireRate, point));
        model.shots.Add(new ShotState { id = 1000, team = 0, position = point });
        controller.Step();
        model.Eliminate(0); model.Tick(.2f); yield return null;
        Assert.IsFalse(source.isPlaying, "A delivery to a destroyed cannon must stay silent.");
        controller.RestartSameSeed(); yield return null;
        Assert.IsFalse(controller.GetComponentInChildren<AudioSource>().isPlaying);
    }

    [UnityTest] public IEnumerator DisablingSoundsMutesExplosionAndBoostReceipt()
    {
        yield return Load(false, false);
        config.celebration.enableSounds = false;
        config.boosts.animateCollection = false;
        config.boosts.firstSpawnDelay = 9999;
        config.cannon.destroyOnEnemyHit = true;
        controller.Rebuild(); controller.Step();
        var model = controller.Model;
        foreach (var team in model.teams) foreach (var ball in team.balls) ball.delay = 9999;
        var point = new Vector2(-1, 1);
        Assert.IsNotNull(model.SpawnBoost(BoostKind.DoubleAmmo, point));
        model.shots.Add(new ShotState { id = 999, team = 0, position = point });
        controller.Step(); yield return null;
        Assert.IsFalse(controller.GetComponentInChildren<AudioSource>().isPlaying);
        model.shots.Add(new ShotState { id = 1000, team = 0, position = model.teams[1].cannonPosition });
        controller.Step(); yield return null;
        Assert.IsFalse(model.teams[1].alive);
        Assert.IsFalse(controller.GetComponentInChildren<AudioSource>().isPlaying);
    }

    [UnityTest] public IEnumerator DrawHasNoWinnerSoundOrConfetti()
    {
        yield return Load(false, false);
        for (int t = 0; t < 4; t++) controller.Model.Eliminate(t);
        controller.Step();
        yield return null;
        Assert.AreEqual(-1, controller.Model.winner);
        Assert.AreEqual("DRAW", Label("Status").text);
        Assert.AreEqual(0, controller.GetComponentsInChildren<ParticleSystem>().Length);
        Assert.IsFalse(controller.GetComponentInChildren<AudioSource>().isPlaying);
    }

    [UnityTest] public IEnumerator DisablingPresentationAndSoundsPreservesOriginalGameFlow()
    {
        yield return Load(false);
        config.celebration.enableVictoryCard = false;
        config.celebration.enableSounds = false;
        controller.Rebuild();
        Assert.AreEqual(MatchPhase.Running, controller.Model.phase);
        controller.Model.Eliminate(0); controller.Model.Eliminate(2); controller.Model.Eliminate(3);
        controller.Step();
        yield return null;
        Assert.AreEqual(MatchPhase.Finished, controller.Model.phase);
        Assert.IsNull(Label("Countdown Text"));
        Assert.IsNull(Label("Status"));
        Assert.AreEqual(0, controller.GetComponentsInChildren<Canvas>().Length);
        Assert.IsFalse(controller.GetComponentInChildren<AudioSource>().isPlaying);
    }
}
