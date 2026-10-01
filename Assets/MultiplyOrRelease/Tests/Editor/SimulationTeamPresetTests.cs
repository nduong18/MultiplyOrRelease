using System;
using System.Reflection;
using MultiplyOrRelease;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public class SimulationTeamPresetTests
{
    SimulationConfig config;
    TeamPreset teamPreset;
    TeamLineupPreset lineup;
    string temporaryAsset;
    readonly float[] inward = { -45, -135, 45, 135 };

    [SetUp] public void Setup()
    {
        config = Object.Instantiate(AssetDatabase.LoadAssetAtPath<SimulationConfig>(
            "Assets/MultiplyOrRelease/Config/DefaultSimulation.asset"));
        teamPreset = ScriptableObject.CreateInstance<TeamPreset>();
        lineup = ScriptableObject.CreateInstance<TeamLineupPreset>();
    }
    [TearDown] public void Cleanup()
    {
        if (!string.IsNullOrEmpty(temporaryAsset)) AssetDatabase.DeleteAsset(temporaryAsset);
        if (teamPreset != null && !AssetDatabase.Contains(teamPreset)) Object.DestroyImmediate(teamPreset);
        if (lineup != null && !AssetDatabase.Contains(lineup)) Object.DestroyImmediate(lineup);
        Object.DestroyImmediate(config);
    }
    void AssertTeam(TeamSettings expected, TeamSettings actual, bool includeAim = true)
    {
        foreach (var field in typeof(TeamSettings).GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!includeAim && field.Name == "aimDegrees") continue;
            Assert.AreEqual(field.GetValue(expected), field.GetValue(actual), field.Name);
        }
        Assert.AreNotSame(expected, actual);
    }

    [Test] public void SingleTeamSnapshotPreservesEveryFieldAndAssetReferenceWithoutSharingSettings()
    {
        teamPreset.CaptureFrom(config, 1);
        AssertTeam(config.teams[1], teamPreset.team);
        Assert.AreEqual(1, teamPreset.savedSlot);
        string name = teamPreset.team.name;
        config.teams[1].name = "Changed config";
        Assert.AreEqual(name, teamPreset.team.name);
        teamPreset.ApplyTo(config, 2);
        AssertTeam(teamPreset.team, config.teams[2], false);
        config.teams[2].sweepSpeed = 777;
        Assert.AreNotEqual(777, teamPreset.team.sweepSpeed);
    }

    [Test] public void LoadingIntoEveryCornerPreservesTheInwardAimOffset()
    {
        for (int source = 0; source < 4; source++)
        {
            config.teams[source].aimDegrees = inward[source] + 17;
            teamPreset.CaptureFrom(config, source);
            for (int destination = 0; destination < 4; destination++)
            {
                teamPreset.ApplyTo(config, destination);
                Assert.AreEqual(17, Mathf.DeltaAngle(inward[destination], config.teams[destination].aimDegrees), .0001f);
                AssertTeam(teamPreset.team, config.teams[destination], false);
            }
        }
    }

    [Test] public void MovingInEitherDirectionKeepsTeamDataAndCornerAimAligned()
    {
        for (int slot = 0; slot < 4; slot++) config.teams[slot].aimDegrees = inward[slot] + slot;
        lineup.CaptureFrom(config);
        for (int from = 0; from < 4; from++) for (int to = 0; to < 4; to++)
        {
            lineup.ApplyTo(config);
            TeamSetup.Move(config, from, to);
            AssertTeam(lineup.teams[from], config.teams[to], false);
            Assert.AreEqual(from, Mathf.DeltaAngle(inward[to], config.teams[to].aimDegrees), .0001f);
            for (int destination = 0; destination < 4; destination++)
            {
                int source = Array.FindIndex(lineup.teams, team => team.name == config.teams[destination].name);
                AssertTeam(lineup.teams[source], config.teams[destination], false);
                Assert.AreEqual(source, Mathf.DeltaAngle(inward[destination], config.teams[destination].aimDegrees), .0001f);
            }
        }
    }

    [Test] public void ReorderingMovesCannonsPlinkoAndStartingTerritoriesTogether()
    {
        string oldName = config.teams[0].name;
        TeamSetup.Move(config, 0, 3);
        var model = new SimulationModel(config, 123);
        Assert.AreEqual(oldName, config.teams[3].name);
        Assert.Greater(model.teams[3].cannonPosition.x, 0);
        Assert.Less(model.teams[3].cannonPosition.y, 0);
        Assert.AreEqual(3, model.owners[config.board.columns - 1]);
        var parent = new GameObject("Team Position Test");
        SimulationView view = null;
        try
        {
            view = new SimulationView(parent.transform, model, true);
            var cannon = parent.transform.Find("Generated Preview/" + oldName + " Cannon");
            var panel = parent.transform.Find("Generated Preview/" + oldName + " Plinko Panel");
            Assert.Greater(cannon.localPosition.x, 0); Assert.Less(cannon.localPosition.y, 0);
            var peg = panel.GetComponentInChildren<SpriteRenderer>();
            Assert.Greater(peg.bounds.center.x, 0); Assert.Less(peg.bounds.center.y, 0);
        }
        finally { view?.Dispose(); Object.DestroyImmediate(parent); }
    }

    [Test] public void CustomStartingOwnershipCanBePreservedOrAlignedExplicitly()
    {
        config.board.quadrantOwners = new[] { 3, 2, 1, 0 };
        TeamSetup.Move(config, 0, 1, false);
        CollectionAssert.AreEqual(new[] { 3, 2, 1, 0 }, config.board.quadrantOwners);
        teamPreset.CaptureFrom(config, 0); teamPreset.ApplyTo(config, 3, false);
        CollectionAssert.AreEqual(new[] { 3, 2, 1, 0 }, config.board.quadrantOwners);
        TeamSetup.Move(config, 0, 1, true);
        CollectionAssert.AreEqual(new[] { 0, 1, 2, 3 }, config.board.quadrantOwners);
    }

    [Test] public void FullLineupRoundTripRestoresOrderDataAndMappingWithoutChangingGameSettings()
    {
        config.board.quadrantOwners = new[] { 1, 0, 3, 2 };
        lineup.CaptureFrom(config);
        float speed = config.projectile.speed, pop = config.cannon.firePopDuration;
        TeamSetup.Move(config, 3, 0);
        config.teams[0].name = "Changed";
        lineup.ApplyTo(config);
        for (int i = 0; i < 4; i++) AssertTeam(lineup.teams[i], config.teams[i]);
        CollectionAssert.AreEqual(new[] { 1, 0, 3, 2 }, config.board.quadrantOwners);
        Assert.AreNotSame(lineup.quadrantOwners, config.board.quadrantOwners);
        Assert.AreEqual(speed, config.projectile.speed); Assert.AreEqual(pop, config.cannon.firePopDuration);
    }

    [Test] public void AssetsRoundTripThroughUnitySerializationWithTheirSpriteAndTextureReferences()
    {
        teamPreset.CaptureFrom(config, 1);
        temporaryAsset = AssetDatabase.GenerateUniqueAssetPath("Assets/MultiplyOrRelease/Tests/Editor/TeamPresetSerializationTest.asset");
        AssetDatabase.CreateAsset(teamPreset, temporaryAsset); AssetDatabase.SaveAssetIfDirty(teamPreset);
        AssetDatabase.ImportAsset(temporaryAsset, ImportAssetOptions.ForceUpdate);
        var saved = AssetDatabase.LoadAssetAtPath<TeamPreset>(temporaryAsset);
        AssertTeam(config.teams[1], saved.team);
        Assert.AreEqual(1, saved.savedSlot);
    }

    [Test] public void MalformedPresetAndInvalidSlotsDoNotPartiallyModifyTheConfig()
    {
        var before = config.teams;
        Assert.Throws<ArgumentOutOfRangeException>(() => TeamSetup.Move(config, -1, 0));
        Assert.AreSame(before, config.teams);
        lineup.CaptureFrom(config); lineup.quadrantOwners = new[] { 9, 0, 1, 2 };
        Assert.Throws<InvalidOperationException>(() => lineup.ApplyTo(config));
        Assert.AreSame(before, config.teams);
        teamPreset.savedSlot = 8;
        Assert.Throws<ArgumentOutOfRangeException>(() => teamPreset.ApplyTo(config, 0));
        Assert.AreSame(before, config.teams);
    }
}
