using System.Linq;
using MultiplyOrRelease;
using NUnit.Framework;
using UnityEngine;

public class SimulationBoostSpawnTests
{
    SimulationConfig config;
    SimulationModel model;

    [SetUp] public void Setup()
    {
        config = ScriptableObject.CreateInstance<SimulationConfig>();
        config.board.size = 8;
        config.board.columns = config.board.rows = 8;
        config.projectile.captureRadiusCells = 0;
        config.boosts.firstSpawnDelay = 0;
        config.boosts.spawnIntervalMin = config.boosts.spawnIntervalMax = 8;
        NewModel();
    }

    [TearDown] public void Cleanup() { Object.DestroyImmediate(config); }

    void NewModel()
    {
        model = new SimulationModel(config, 4321); model.Start();
        foreach (var team in model.teams) foreach (var ball in team.balls) ball.delay = 9999;
    }

    void Fill(int owner)
    {
        for (int y = 0; y < config.board.rows; y++)
            for (int x = 0; x < config.board.columns; x++) model.Capture(x, y, owner);
    }

    int OwnerAt(Vector2 point)
    {
        int x = Mathf.FloorToInt((point.x + config.board.size * .5f) / model.CellWidth);
        int y = Mathf.FloorToInt((point.y + config.board.size * .5f) / model.CellHeight);
        return model.owners[y * config.board.columns + x];
    }

    [Test] public void SmallTerritoryIslandIsPreferredAndFollowsOwnershipChanges()
    {
        Fill(0);
        // Team 1's remaining island is in team 2's original quadrant.
        model.Capture(2, 2, 1);
        model.Tick(0);
        Assert.AreEqual(1, model.boosts.Count);
        Assert.AreEqual(1, OwnerAt(model.boosts[0].position));
        model.boosts.Clear();
        model.Capture(2, 2, 0);
        model.Capture(5, 2, 2);
        model.Tick(8);
        Assert.AreEqual(1, model.boosts.Count);
        Assert.AreEqual(2, OwnerAt(model.boosts[0].position));
    }

    [Test] public void TiedTeamsAllGetSpawnsAndSameSeedReplaysTheChoice()
    {
        var other = new SimulationModel(config, 4321); other.Start();
        foreach (var team in other.teams) foreach (var ball in team.balls) ball.delay = 9999;
        var counts = new int[4];
        for (int i = 0; i < 64; i++)
        {
            model.Tick(8); other.Tick(8);
            Assert.AreEqual(1, model.boosts.Count);
            Assert.AreEqual(model.boosts[0].position, other.boosts[0].position);
            Assert.AreEqual(model.boosts[0].kind, other.boosts[0].kind);
            counts[OwnerAt(model.boosts[0].position)]++;
            model.boosts.Clear(); other.boosts.Clear();
        }
        Assert.IsTrue(counts.All(count => count > 0), "A tie must not always prefer the lowest team index.");
    }

    [Test] public void EliminatedSmallestTeamIsExcludedFromPreference()
    {
        Fill(0);
        model.Capture(2, 2, 1); model.Capture(5, 2, 2);
        model.Eliminate(1);
        model.Tick(0);
        Assert.AreEqual(1, model.boosts.Count);
        Assert.AreEqual(2, OwnerAt(model.boosts[0].position));
    }

    [Test] public void BlockedSmallestTerritoryFallsBackToNextLivingTeam()
    {
        Fill(0);
        model.Capture(0, 7, 1); // Too close to a cannon to spawn safely.
        model.Capture(2, 2, 2); model.Capture(3, 2, 2);
        model.Tick(0);
        Assert.AreEqual(1, model.boosts.Count);
        Assert.AreEqual(2, OwnerAt(model.boosts[0].position));
    }

    [Test] public void OccupiedSmallestTerritoryFallsBackWithoutOverlappingPickups()
    {
        config.boosts.radius = .49f; NewModel();
        Fill(0);
        model.Capture(2, 2, 1);
        model.Capture(4, 4, 2); model.Capture(5, 4, 2);
        Vector2 occupied = new Vector2(-1.5f, -1.5f);
        Assert.IsNotNull(model.SpawnBoost(BoostKind.DoubleAmmo, occupied));
        model.Tick(0);
        Assert.AreEqual(2, model.boosts.Count);
        Assert.AreEqual(2, OwnerAt(model.boosts[1].position));
        Assert.GreaterOrEqual(Vector2.Distance(occupied, model.boosts[1].position), config.boosts.radius * 2);
    }

    [Test] public void NoUsableLivingTerritoryStillAllowsArenaFallback()
    {
        Fill(3); model.Eliminate(3);
        model.Tick(0);
        Assert.AreEqual(1, model.boosts.Count);
        Assert.AreEqual(3, OwnerAt(model.boosts[0].position));
    }

    [Test] public void TwoTeamsHalvePendingWaitAndEveryFollowingInterval()
    {
        model.Tick(0); model.boosts.Clear(); // Next normal spawn is at t=8.
        model.Tick(2);
        model.Eliminate(2); model.Eliminate(3); // Remaining 6 seconds becomes 3: t=5.
        model.Tick(2.99f); Assert.AreEqual(0, model.boosts.Count);
        model.Tick(.02f); Assert.AreEqual(1, model.boosts.Count);
        model.boosts.Clear();
        model.Tick(3.99f); Assert.AreEqual(0, model.boosts.Count);
        model.Tick(.02f); Assert.AreEqual(1, model.boosts.Count);
        Assert.AreEqual(8, config.boosts.spawnIntervalMin);
        Assert.AreEqual(8, config.boosts.spawnIntervalMax);
    }

    [Test] public void ThreeTeamsKeepNormalTimingAndTwoTeamsCanKeepItWhenConfigured()
    {
        config.boosts.twoTeamSpawnIntervalMultiplier = 1;
        model.Tick(0); model.boosts.Clear();
        model.Eliminate(2);
        model.Tick(4); Assert.AreEqual(0, model.boosts.Count);
        model.Eliminate(3);
        model.Tick(3.99f); Assert.AreEqual(0, model.boosts.Count);
        model.Tick(.02f); Assert.AreEqual(1, model.boosts.Count);
    }

    [Test] public void TwoTeamsAlsoShortenPendingFirstSpawnAndStopAtOneSurvivor()
    {
        config.boosts.firstSpawnDelay = 8; NewModel();
        model.Tick(2); Assert.AreEqual(0, model.boosts.Count);
        model.Eliminate(2); model.Eliminate(3);
        model.Tick(3); Assert.AreEqual(1, model.boosts.Count);
        model.Eliminate(1); model.Tick(4);
        Assert.AreEqual(0, model.boosts.Count);
        Assert.AreNotEqual(MatchPhase.Running, model.phase);
    }

    [Test] public void TerritoryPreferenceCanBeDisabledForUniformArenaSpawning()
    {
        config.boosts.preferSmallerTerritories = false;
        Fill(0); model.Capture(2, 2, 1);
        model.Tick(0);
        Assert.AreEqual(1, model.boosts.Count);
        Assert.AreEqual(0, OwnerAt(model.boosts[0].position));
    }
}
