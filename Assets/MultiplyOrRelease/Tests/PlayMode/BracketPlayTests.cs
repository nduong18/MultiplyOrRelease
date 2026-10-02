using System.Collections;
using MultiplyOrRelease;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class BracketPlayTests
{
    BracketController bracket;
    BracketConfig config;
    SimulationConfig simulationConfig;
    IEnumerator Load(bool quick = false)
    {
        SceneManager.LoadScene("Bracket");
        yield return null;
        bracket = Object.FindFirstObjectByType<BracketController>();
        Assert.IsNotNull(bracket);
        config = Object.Instantiate(bracket.config);
        simulationConfig = Object.Instantiate(config.simulation);
        config.simulation = simulationConfig;
        if (quick)
        {
            config.flagMoveDelay = 0;
            config.flagMoveDuration = .05f; config.matchStartDelay = .05f;
            config.winnerCardHoldDuration = .15f;
            simulationConfig.celebration.victoryCardDelay = .1f;
        }
        simulationConfig.celebration.enableSounds = false;
        simulationConfig.celebration.enableVictoryConfetti = true;
        bracket.config = config; bracket.Rebuild();
        yield return null;
    }
    IEnumerator WaitForPhase(BracketPhase phase, float timeout = 4)
    {
        float deadline = Time.realtimeSinceStartup + timeout;
        while (bracket.Phase != phase && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.AreEqual(phase, bracket.Phase);
    }
    void FinishWithWinner(int localWinner)
    {
        bracket.simulation.Step();
        for (int i = 0; i < 4; i++) if (i != localWinner) bracket.simulation.Model.Eliminate(i);
        bracket.simulation.Step();
    }
    [TearDown] public void Cleanup()
    {
        if (bracket != null) bracket.enabled = false;
        if (config != null) Object.Destroy(config);
        if (simulationConfig != null) Object.Destroy(simulationConfig);
    }
    [UnityTest] public IEnumerator BracketWaitsThreeSecondsSlidesOneSecondThenHoldsTwoSecondsBeforeGameplay()
    {
        yield return Load();
        Assert.AreEqual(BracketPhase.PreparingMatch, bracket.Phase);
        Assert.AreEqual(0, bracket.ActiveMatch);
        var root = bracket.transform.Find("Bracket Presentation");
        Assert.AreEqual(0, root.GetComponentsInChildren<Button>(true).Length);
        Assert.IsFalse(bracket.simulation.gameObject.activeSelf);
        var flag = root.Find("Bracket Board/Team Slot 0").GetComponent<RectTransform>();
        float from = flag.anchoredPosition.x;
        Assert.AreEqual(3, config.flagMoveDelay);
        Assert.AreEqual(1, config.flagMoveDuration);
        Assert.AreEqual(2, config.matchStartDelay);
        yield return new WaitForSecondsRealtime(2.6f);
        Assert.AreEqual(from, flag.anchoredPosition.x, .01f, "Flags stay still during the initial three-second hold.");
        Assert.IsFalse(bracket.simulation.gameObject.activeSelf);
        Assert.IsFalse(root.Find("Bracket Board/Slot Number 0").gameObject.activeSelf);
        yield return new WaitForSecondsRealtime(.8f);
        Assert.Greater(flag.anchoredPosition.x, from);
        Assert.Less(flag.anchoredPosition.x, -500, "The movement takes one second.");
        yield return new WaitForSecondsRealtime(.7f);
        Assert.AreEqual(-500, flag.anchoredPosition.x, .01f);
        Assert.Greater(flag.anchoredPosition.x, from);
        for (int i = 0; i < 4; i++)
        {
            var number = root.Find("Bracket Board/Slot Number " + i);
            Assert.IsTrue(number.gameObject.activeInHierarchy);
            Assert.AreEqual((i + 1).ToString(), number.Find("Number").GetComponent<Text>().text);
        }
        yield return new WaitForSecondsRealtime(1.45f);
        Assert.IsFalse(bracket.simulation.gameObject.activeSelf, "The two-second hold follows the flag animation.");
        yield return WaitForPhase(BracketPhase.PlayingMatch);
        Assert.IsTrue(bracket.simulation.CountdownActive, "Each match retains its configured 321 GO countdown.");
        Assert.AreEqual(MatchPhase.Ready, bracket.simulation.Model.phase);
        Assert.IsFalse(root.Find("Background").gameObject.activeSelf);
        var countdown = bracket.simulation.transform.Find("Match Presentation/Start Countdown/Countdown Text").GetComponent<Text>();
        var settings = config.simulation.celebration;
        yield return new WaitForSecondsRealtime(settings.countdownStartDelay + settings.countdownAnimationDelay + .1f);
        foreach (string value in new[] { "3", "2", "1", "GO!" })
        {
            Assert.AreEqual(value, countdown.text);
            Assert.AreEqual(MatchPhase.Ready, bracket.simulation.Model.phase, "Gameplay waits until GO finishes.");
            yield return new WaitForSecondsRealtime(settings.countdownStepDuration);
        }
        Assert.IsFalse(bracket.simulation.CountdownActive);
        Assert.AreEqual(MatchPhase.Running, bracket.simulation.Model.phase);
    }
    [UnityTest] public IEnumerator WinnerCardStaysFiveSecondsBeforeAutomaticReturn()
    {
        yield return Load();
        config.flagMoveDelay = 0;
        config.flagMoveDuration = .05f; config.matchStartDelay = 0;
        bracket.Rebuild();
        yield return WaitForPhase(BracketPhase.PlayingMatch);
        FinishWithWinner(0);
        Assert.AreEqual(MatchPhase.Finished, bracket.simulation.Model.phase);
        Assert.IsFalse(bracket.simulation.ResultCardVisible);
        yield return WaitForPhase(BracketPhase.ShowingResult);
        Assert.IsTrue(bracket.simulation.ResultCardVisible);
        yield return new WaitForSecondsRealtime(4.5f);
        Assert.AreEqual(BracketPhase.ShowingResult, bracket.Phase);
        Assert.IsNull(bracket.State.GroupWinner(0));
        yield return new WaitForSecondsRealtime(.65f);
        Assert.AreSame(bracket.State.TeamAt(0), bracket.State.GroupWinner(0));
        Assert.AreEqual(2, bracket.ActiveMatch, "Top right follows top left.");
    }
    [UnityTest] public IEnumerator TournamentAutomaticallyRunsAllMatchesThenCelebratesChampion()
    {
        yield return Load(true);
        string originalName = simulationConfig.teams[0].name;
        foreach (int match in new[] { 0, 2, 1, 3, 4 })
        {
            yield return WaitForPhase(BracketPhase.PlayingMatch);
            Assert.AreEqual(match, bracket.ActiveMatch);
            Assert.IsTrue(bracket.simulation.CountdownActive, "Countdown runs for group matches and the final.");
            var participants = bracket.State.Participants(match);
            for (int i = 0; i < 4; i++) Assert.AreEqual(participants[i].team.name, bracket.simulation.Model.config.teams[i].name);
            FinishWithWinner(0);
            yield return WaitForPhase(BracketPhase.ShowingResult);
            Assert.IsTrue(bracket.simulation.ResultCardVisible);
            yield return new WaitForSecondsRealtime(.08f);
            Assert.AreEqual(BracketPhase.ShowingResult, bracket.Phase);
            yield return WaitForPhase(match == 4 ? BracketPhase.Champion : BracketPhase.PreparingMatch);
        }
        Assert.AreSame(bracket.State.TeamAt(0), bracket.State.Champion);
        Assert.AreEqual(-1, bracket.ActiveMatch); Assert.AreEqual(-1, bracket.State.NextMatch);
        Assert.IsFalse(bracket.simulation.gameObject.activeSelf);
        Assert.AreEqual(originalName, simulationConfig.teams[0].name);
        var board = bracket.transform.Find("Bracket Presentation/Bracket Board");
        Assert.IsTrue(board.gameObject.activeInHierarchy);
        Assert.AreEqual(bracket.State.Champion.team.cannonSprite, board.Find("Champion/Champion Flag").GetComponent<Image>().sprite);
        Assert.AreEqual(bracket.State.Champion.team.name, board.Find("Champion Name").GetComponent<Text>().text);
        Assert.IsTrue(board.Find("Champion Name").gameObject.activeInHierarchy);
        yield return new WaitForSecondsRealtime(.12f);
        int particles = 0;
        foreach (var effect in bracket.GetComponentsInChildren<ParticleSystem>()) particles += effect.particleCount;
        Assert.Greater(particles, 0);
        Assert.IsNull(bracket.transform.Find("Bracket Champion Celebration/Victory Card"));
        bracket.Rebuild(); yield return null;
        Assert.IsNull(bracket.State.Champion);
        Assert.IsFalse(bracket.transform.Find("Bracket Presentation/Bracket Board/Champion Name").gameObject.activeSelf);
        Assert.IsNull(bracket.transform.Find("Bracket Champion Celebration"));
    }
    [UnityTest] public IEnumerator DrawAutomaticallyReplaysSameGroupWithNewSeed()
    {
        yield return Load(true);
        yield return WaitForPhase(BracketPhase.PlayingMatch);
        int seed = bracket.simulation.CurrentSeed;
        bracket.simulation.Step();
        for (int i = 0; i < 4; i++) bracket.simulation.Model.Eliminate(i);
        bracket.simulation.Step();
        yield return WaitForPhase(BracketPhase.ShowingResult);
        yield return WaitForPhase(BracketPhase.PreparingMatch);
        Assert.AreEqual(0, bracket.ActiveMatch); Assert.IsNull(bracket.State.GroupWinner(0));
        yield return WaitForPhase(BracketPhase.PlayingMatch);
        Assert.AreNotEqual(seed, bracket.simulation.CurrentSeed);
    }
}
