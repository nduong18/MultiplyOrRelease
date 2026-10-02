using System;
using UnityEngine;

namespace MultiplyOrRelease
{
    // Runtime copies keep tournament edits/results out of the saved team assets.
    public sealed class BracketState
    {
        readonly TeamPreset[] entrants;
        readonly bool withinCapacity;
        readonly int[] winners = { -1, -1, -1, -1 };
        public int ChampionSlot { get; private set; } = -1;
        public TeamPreset Champion => ChampionSlot < 0 ? null : entrants[ChampionSlot];
        public BracketState(TeamPreset[] teams)
        {
            entrants = new TeamPreset[16];
            withinCapacity = teams == null || teams.Length <= 16;
            if (teams != null) Array.Copy(teams, entrants, Mathf.Min(teams.Length, entrants.Length));
        }
        public bool IsReady
        {
            get
            {
                if (!withinCapacity) return false;
                for (int i = 0; i < 16; i++)
                {
                    if (entrants[i] == null || entrants[i].team == null) return false;
                    for (int j = 0; j < i; j++)
                        if (entrants[j] == entrants[i]) return false;
                }
                return true;
            }
        }
        public TeamPreset TeamAt(int slot) => entrants[slot];
        public TeamPreset GroupWinner(int group) => winners[group] < 0 ? null : entrants[winners[group]];
        public int NextMatch
        {
            get
            {
                if (!IsReady) return -1;
                // Read the bracket left to right, then top to bottom.
                foreach (int g in new[] { 0, 2, 1, 3 }) if (winners[g] < 0) return g;
                return ChampionSlot < 0 ? 4 : -1;
            }
        }
        public bool CanPlay(int match)
        {
            if (!IsReady || match < 0 || match > 4) return false;
            if (match < 4) return true;
            foreach (int winner in winners) if (winner < 0) return false;
            return true;
        }
        public TeamPreset[] Participants(int match)
        {
            if (!CanPlay(match)) throw new InvalidOperationException("Choose 16 distinct teams and complete the group matches before the final.");
            var result = new TeamPreset[4];
            for (int i = 0; i < 4; i++) result[i] = entrants[match == 4 ? winners[i] : match * 4 + i];
            return result;
        }
        public void RecordWinner(int match, int localWinner)
        {
            if (localWinner < 0 || localWinner > 3 || !CanPlay(match))
                throw new ArgumentOutOfRangeException(nameof(localWinner));
            if (match == 4) ChampionSlot = winners[localWinner];
            else { winners[match] = match * 4 + localWinner; ChampionSlot = -1; }
        }
        public void SetTeam(int slot, TeamPreset team)
        {
            if (slot < 0 || slot >= 16 || (team != null && team.team == null)) throw new ArgumentException("Invalid team slot.");
            if (entrants[slot] == team) return;
            int other = team != null ? Array.IndexOf(entrants, team) : -1;
            if (other >= 0) entrants[other] = entrants[slot]; // Move an existing entrant by swapping.
            entrants[slot] = team;
            ResetResults();
        }
        public void Shuffle(int seed)
        {
            var random = new System.Random(seed);
            for (int i = 15; i > 0; i--)
            {
                int j = random.Next(i + 1);
                var team = entrants[i]; entrants[i] = entrants[j]; entrants[j] = team;
            }
            ResetResults();
        }
        public void ResetResults()
        {
            for (int i = 0; i < 4; i++) winners[i] = -1;
            ChampionSlot = -1;
        }
        public SimulationConfig CreateMatchConfig(SimulationConfig source, int match)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var participants = Participants(match);
            var session = UnityEngine.Object.Instantiate(source);
            session.name = "Bracket " + (match == 4 ? "Final" : "Match " + (match + 1));
            session.hideFlags = HideFlags.HideAndDontSave;
            session.teams = new TeamSettings[4];
            for (int i = 0; i < 4; i++)
                session.teams[i] = TeamSetup.ForSlot(participants[i].team, participants[i].savedSlot, i);
            TeamSetup.AlignStartingTerritories(session);
            session.autoStart = true;
            return session;
        }
    }
}
