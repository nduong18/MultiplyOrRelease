using System.Linq;
using MultiplyOrRelease;
using NUnit.Framework;
using UnityEngine;

public class BracketStateTests
{
    TeamPreset[] teams;
    BracketState bracket;
    [SetUp] public void Setup()
    {
        teams = Enumerable.Range(0, 16).Select(i =>
        {
            var t = ScriptableObject.CreateInstance<TeamPreset>(); t.team.name = "Team " + i; return t;
        }).ToArray();
        bracket = new BracketState(teams);
    }
    [TearDown] public void Cleanup() { foreach (var team in teams) Object.DestroyImmediate(team); }

    [Test] public void FourGroupWinnersFeedTheFinalAndChampionKeepsOriginalIdentity()
    {
        Assert.IsFalse(bracket.CanPlay(4));
        Assert.Throws<System.InvalidOperationException>(() => bracket.Participants(4));
        int[] picks = { 3, 0, 2, 1 };
        foreach (int g in new[] { 0, 2, 1, 3 })
        {
            Assert.AreEqual(g, bracket.NextMatch);
            bracket.RecordWinner(g, picks[g]);
            Assert.AreSame(teams[g * 4 + picks[g]], bracket.GroupWinner(g));
        }
        CollectionAssert.AreEqual(new[] { teams[3], teams[4], teams[10], teams[13] }, bracket.Participants(4));
        Assert.AreEqual(4, bracket.NextMatch);
        bracket.RecordWinner(4, 2);
        Assert.AreSame(teams[10], bracket.Champion); Assert.AreEqual(-1, bracket.NextMatch);
        bracket.RecordWinner(0, 1);
        Assert.IsNull(bracket.Champion, "Replaying a group invalidates the old champion.");
    }
    [Test] public void ChoosingAnExistingEntrantSwapsSlotsAndClearsResultsWithoutEditingAssets()
    {
        bracket.RecordWinner(0, 0); bracket.SetTeam(0, teams[15]);
        Assert.AreSame(teams[15], bracket.TeamAt(0)); Assert.AreSame(teams[0], bracket.TeamAt(15));
        Assert.AreEqual(16, Enumerable.Range(0, 16).Select(bracket.TeamAt).Distinct().Count());
        Assert.IsNull(bracket.GroupWinner(0)); Assert.AreSame(teams[0], new BracketState(teams).TeamAt(0));
        Assert.AreEqual("Team 0", teams[0].team.name);
    }
    [Test] public void ShuffleIsRepeatableAndPreservesAllTeams()
    {
        var other = new BracketState(teams); bracket.Shuffle(7); other.Shuffle(7);
        CollectionAssert.AreEqual(Enumerable.Range(0, 16).Select(bracket.TeamAt), Enumerable.Range(0, 16).Select(other.TeamAt));
        CollectionAssert.AreEquivalent(teams, Enumerable.Range(0, 16).Select(bracket.TeamAt));
        Assert.AreEqual(0, bracket.NextMatch);
    }
    [Test] public void MatchConfigUsesCopiesAndRotatesAimToEachMapCorner()
    {
        var config = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<SimulationConfig>("Assets/MultiplyOrRelease/Config/DefaultSimulation.asset"));
        var original = config.teams[0]; var session = bracket.CreateMatchConfig(config, 0);
        try
        {
            for (int i = 0; i < 4; i++)
            {
                Assert.AreEqual("Team " + i, session.teams[i].name);
                Assert.AreNotSame(teams[i].team, session.teams[i]);
            }
            Assert.AreEqual(-135, session.teams[1].aimDegrees);
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3 }, session.board.quadrantOwners);
            session.teams[0].name = "Changed";
            Assert.AreEqual("Team 0", teams[0].team.name); Assert.AreSame(original, config.teams[0]);
        }
        finally { Object.DestroyImmediate(session); Object.DestroyImmediate(config); }
    }
    [Test] public void DuplicateAndEmptyEntrantsAreRejected()
    {
        var invalid = (TeamPreset[])teams.Clone(); invalid[15] = teams[0];
        Assert.Throws<System.ArgumentException>(() => new BracketState(invalid));
        invalid[15] = null;
        Assert.Throws<System.ArgumentException>(() => new BracketState(invalid));
    }
}
