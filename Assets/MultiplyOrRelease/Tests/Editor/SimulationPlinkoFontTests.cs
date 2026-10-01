using System;
using System.Collections.Generic;
using MultiplyOrRelease;
using NUnit.Framework;
using UnityEngine;

public class SimulationPlinkoFontTests
{
    SimulationConfig config;
    SimulationModel model;
    SimulationView view;
    GameObject parent;
    Font customFont;

    [SetUp] public void SetUp()
    {
        config = UnityEngine.Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<SimulationConfig>(
            "Assets/MultiplyOrRelease/Config/DefaultSimulation.asset"));
        config.board.columns = config.board.rows = 8;
        config.presentation.showTeamNames = true;
        config.presentation.showReleaseCountdown = true;
        config.plinko.font = null;
        config.plinko.ammoTextOffset = config.plinko.teamNameOffset = Vector2.zero;
        config.plinko.multiplyTextOffset = config.plinko.releaseTextOffset = config.plinko.statusTextOffset = Vector2.zero;
        model = new SimulationModel(config, 1207);
        parent = new GameObject("Plinko Font Test");
    }

    [TearDown] public void TearDown()
    {
        view?.Dispose();
        view = null;
        if (parent != null) UnityEngine.Object.DestroyImmediate(parent);
        if (config != null) UnityEngine.Object.DestroyImmediate(config);
        if (customFont != null) UnityEngine.Object.DestroyImmediate(customFont);
    }

    void Build() { view?.Dispose(); view = new SimulationView(parent.transform, model, true); }

    void AssertFonts(Font expected)
    {
        var texts = parent.GetComponentsInChildren<TextMesh>(true);
        Assert.AreEqual(20, texts.Length);
        foreach (var text in texts)
        {
            Assert.AreSame(expected, text.font, text.name);
            Assert.AreSame(expected.material, text.GetComponent<MeshRenderer>().sharedMaterial, text.name);
        }
    }

    [Test] public void EmptyPlinkoFontUsesPresentationFontAndMaterial()
    {
        Build();
        AssertFonts(config.presentation.font);
    }

    [Test] public void OverrideChangesEveryPlinkoLabelAndSurvivesAmmoUpdatesWithoutChangingGlobalFont()
    {
        var globalFont = config.presentation.font;
        customFont = Font.CreateDynamicFontFromOSFont("Arial", 96);
        Assert.IsNotNull(customFont);
        config.plinko.font = customFont;
        Build();
        AssertFonts(customFont);
        model.teams[0].queued = 7;
        view.Render();
        AssertFonts(customFont);
        var ammo = Array.Find(parent.GetComponentsInChildren<TextMesh>(true),
            t => t.name == "Stored Ammo" && t.text == "7");
        Assert.IsNotNull(ammo);
        Assert.AreSame(globalFont, config.presentation.font);
    }

    [Test] public void RemovingOverrideAndRebuildingRestoresGlobalFont()
    {
        customFont = Font.CreateDynamicFontFromOSFont("Arial", 96);
        config.plinko.font = customFont;
        Build();
        AssertFonts(customFont);
        config.plinko.font = null;
        Build();
        AssertFonts(config.presentation.font);
    }

    Vector3 ExpectedOffset(string name)
    {
        switch (name)
        {
            case "Stored Ammo": return config.plinko.ammoTextOffset;
            case "Team Name": return config.plinko.teamNameOffset;
            case "Multiply Label": return config.plinko.multiplyTextOffset;
            case "Release Label": return config.plinko.releaseTextOffset;
            case "Team Status": return config.plinko.statusTextOffset;
            default: throw new ArgumentException(name);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void IndependentOffsetsMoveAllPanelsInTheSameDirectionAndSurviveAmmoUpdates(bool mirror)
    {
        config.plinko.mirrorRightBoards = mirror;
        Build();
        var positions = new Dictionary<string, Vector3>();
        foreach (var text in parent.GetComponentsInChildren<TextMesh>(true))
            positions.Add(text.transform.parent.name + "/" + text.name, text.transform.localPosition);
        config.plinko.ammoTextOffset = new Vector2(.15f, .6f);
        config.plinko.teamNameOffset = new Vector2(-.12f, -.25f);
        config.plinko.multiplyTextOffset = new Vector2(.08f, -.05f);
        config.plinko.releaseTextOffset = new Vector2(-.07f, .04f);
        config.plinko.statusTextOffset = new Vector2(.11f, -.09f);
        Build();
        model.teams[0].queued = 9;
        view.Render();
        foreach (var text in parent.GetComponentsInChildren<TextMesh>(true))
        {
            var before = positions[text.transform.parent.name + "/" + text.name];
            var expected = before + ExpectedOffset(text.name);
            Assert.Less((expected - text.transform.localPosition).sqrMagnitude, .00000001f, text.name);
        }
    }
}
