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
