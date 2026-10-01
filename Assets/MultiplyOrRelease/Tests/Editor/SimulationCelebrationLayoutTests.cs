using MultiplyOrRelease;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public class SimulationCelebrationLayoutTests
{
    SimulationConfig config;
    SimulationController controller;
    GameObject host;
    [SetUp] public void Setup()
    {
        config = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<SimulationConfig>(
            "Assets/MultiplyOrRelease/Config/DefaultSimulation.asset"));
        config.board.columns = config.board.rows = 8;
        host = new GameObject("Celebration Preview Test");
        controller = host.AddComponent<SimulationController>();
        controller.config = config;
    }
    [TearDown] public void Cleanup()
    {
        Object.DestroyImmediate(host); Object.DestroyImmediate(config);
    }
    [Test] public void OriginalCountdownAndWinnerLayoutAndFontAreAvailableInEditor()
    {
        controller.PreviewCountdown();
        var intro = host.transform.Find("Match Presentation/Start Countdown/Countdown Text").GetComponent<Text>();
        Assert.AreEqual("3", intro.text); Assert.AreEqual(200, intro.fontSize);
        Assert.AreSame(config.celebration.countdownFont, intro.font);
        Assert.AreEqual(new Vector2(6, -6), intro.GetComponent<Outline>().effectDistance);
        Assert.AreEqual(0, config.celebration.victoryBackdropColor.a);
        controller.PreviewWinnerCard();
        var card = host.transform.Find("Match Presentation/Victory Card/Card").GetComponent<RectTransform>();
        Assert.AreEqual(new Vector2(700, 600), card.sizeDelta);
        Assert.AreEqual(new Vector2(200, 200), card.Find("Flag").GetComponent<RectTransform>().sizeDelta);
        Assert.IsTrue((card.gameObject.hideFlags & HideFlags.DontSave) != 0);
        Assert.IsNotNull(config.celebration.countdownClip);
        Assert.IsNotNull(config.celebration.winnerClip);
        Assert.IsNotNull(config.celebration.glowShader);
        controller.ClearCelebrationPreview();
        Assert.IsNull(host.transform.Find("Match Presentation"));
    }
}
