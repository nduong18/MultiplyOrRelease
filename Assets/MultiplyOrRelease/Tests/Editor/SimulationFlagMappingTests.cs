using System;
using MultiplyOrRelease;
using NUnit.Framework;
using UnityEngine;

public class SimulationFlagMappingTests
{
    SimulationConfig config;
    SimulationModel model;
    SimulationView view;
    GameObject parent;
    Mesh grid;

    [SetUp] public void SetUp()
    {
        var preset = UnityEditor.AssetDatabase.LoadAssetAtPath<SimulationConfig>("Assets/MultiplyOrRelease/Config/DefaultSimulation.asset");
        config = UnityEngine.Object.Instantiate(preset);
        config.board.columns = config.board.rows = 8;
        config.board.style = TerritoryStyle.Flag;
        config.board.flagMapping = FlagMapping.FitOwnedTerritory;
        config.projectile.captureRadiusCells = 0;
        model = new SimulationModel(config, 1207);
        parent = new GameObject("Flag Mapping Test");
        view = new SimulationView(parent.transform, model, true);
        grid = Array.Find(parent.GetComponentsInChildren<MeshFilter>(), f => f.sharedMesh.name == "Territory Cells").sharedMesh;
    }
    [TearDown] public void TearDown()
    {
        view?.Dispose();
        if (parent != null) UnityEngine.Object.DestroyImmediate(parent);
        if (config != null) UnityEngine.Object.DestroyImmediate(config);
    }
    Vector2 UV(int x, int y, int corner = 0) => grid.uv[(y * config.board.columns + x) * 4 + corner];
    void AssertUV(Vector2 expected, Vector2 actual)
    {
        Assert.AreEqual(expected.x, actual.x, .00001f);
        Assert.AreEqual(expected.y, actual.y, .00001f);
    }
    void Fill(int owner)
    {
        for (int y = 0; y < config.board.rows; y++)
            for (int x = 0; x < config.board.columns; x++) model.Capture(x, y, owner);
    }
    [Test] public void StartingTerritoriesEachContainOneCompleteFlag()
    {
        for (int team = 0; team < 4; team++)
        {
            int x = team % 2 * 4, y = team < 2 ? 4 : 0;
            Vector2 origin = new Vector2(team % 2, team / 2) * .5f;
            AssertUV(origin + Vector2.one * .001f, UV(x, y));
            AssertUV(origin + Vector2.one * .499f, UV(x + 3, y + 3, 2));
        }
    }
    [Test] public void CapturesExpandAndShrinkTheSameFlagWithoutRepeating()
    {
        AssertUV(new Vector2(.375f, .25f), UV(3, 6));
        model.Capture(4, 7, 0); view.Render();
        AssertUV(new Vector2(.3f, .25f), UV(3, 6));
        AssertUV(new Vector2(.4f, .375f), UV(4, 7));
        AssertUV(new Vector2(.499f, .499f), UV(4, 7, 2));
        model.Capture(4, 7, 1); view.Render();
        AssertUV(new Vector2(.375f, .25f), UV(3, 6));
    }
    [Test] public void DisconnectedPiecesShareOneFlagMapping()
    {
        Fill(1); model.Capture(1, 2, 0); model.Capture(6, 5, 0); view.Render();
        AssertUV(new Vector2(.001f, .001f), UV(1, 2));
        AssertUV(new Vector2(5f / 12, .375f), UV(6, 5));
        AssertUV(new Vector2(.499f, .499f), UV(6, 5, 2));
    }
    [Test] public void WholeBoardAndSingleCellRemainValidWhenOtherTeamsOwnNothing()
    {
        Fill(0); view.Render();
        AssertUV(new Vector2(.001f, .001f), UV(0, 0));
        AssertUV(new Vector2(.499f, .499f), UV(7, 7, 2));
        Fill(1); model.Capture(3, 2, 0); view.Render();
        AssertUV(new Vector2(.001f, .001f), UV(3, 2));
        AssertUV(new Vector2(.499f, .499f), UV(3, 2, 2));
        foreach (var uv in grid.uv)
        {
            Assert.IsFalse(float.IsNaN(uv.x) || float.IsInfinity(uv.x));
            Assert.IsFalse(float.IsNaN(uv.y) || float.IsInfinity(uv.y));
        }
    }
    [Test] public void LegacyMappingsAndColorToggleStillWork()
    {
        Assert.AreEqual(0, (int)FlagMapping.EntireArena);
        Assert.AreEqual(1, (int)FlagMapping.RepeatStartingQuadrant);
        model.Capture(4, 4, 0);
        config.board.flagMapping = FlagMapping.RepeatStartingQuadrant; view.Render();
        AssertUV(new Vector2(.001f, .001f), UV(4, 4));
        config.board.flagMapping = FlagMapping.EntireArena; view.Render();
        AssertUV(new Vector2(.25f, .25f), UV(4, 4));
        config.board.flagMapping = FlagMapping.FitOwnedTerritory; view.Render();
        AssertUV(new Vector2(.4f, .001f), UV(4, 4));
        config.board.style = TerritoryStyle.Color; view.Render();
        foreach (var uv in grid.uv) Assert.AreEqual(Vector2.zero, uv);
        config.board.style = TerritoryStyle.Flag; view.Render();
        AssertUV(new Vector2(.4f, .001f), UV(4, 4));
    }
}
