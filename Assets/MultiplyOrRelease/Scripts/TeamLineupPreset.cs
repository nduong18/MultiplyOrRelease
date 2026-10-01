using System;
using UnityEngine;

namespace MultiplyOrRelease
{
    [CreateAssetMenu(menuName = "Multiply or Release/Team Lineup Preset")]
    public sealed class TeamLineupPreset : ScriptableObject
    {
        [Tooltip("Order: upper left, upper right, lower left, lower right.")]
        public TeamSettings[] teams = {
            new TeamSettings { aimDegrees = -45 }, new TeamSettings { aimDegrees = -135 },
            new TeamSettings { aimDegrees = 45 }, new TeamSettings { aimDegrees = 135 }
        };
        public int[] quadrantOwners = { 0, 1, 2, 3 };

        static TeamSettings[] CopyTeams(TeamSettings[] source)
        {
            if (source == null || source.Length != 4)
                throw new InvalidOperationException("A lineup preset must contain exactly four teams.");
            var result = new TeamSettings[4];
            for (int i = 0; i < 4; i++)
            {
                if (source[i] == null) throw new InvalidOperationException("A lineup preset has an empty team slot.");
                result[i] = source[i].Copy();
            }
            return result;
        }
        public void CaptureFrom(SimulationConfig config)
        {
            TeamSetup.CheckConfig(config);
            teams = CopyTeams(config.teams);
            quadrantOwners = config.board.quadrantOwners != null && config.board.quadrantOwners.Length == 4
                ? (int[])config.board.quadrantOwners.Clone() : new[] { 0, 1, 2, 3 };
        }
        public void ApplyTo(SimulationConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            var copiedTeams = CopyTeams(teams);
            if (quadrantOwners == null || quadrantOwners.Length != 4)
                throw new InvalidOperationException("A lineup preset must contain four starting-territory owners.");
            foreach (int owner in quadrantOwners)
                if (owner < 0 || owner > 3) throw new InvalidOperationException("Invalid starting-territory owner.");
            config.teams = copiedTeams; config.board.quadrantOwners = (int[])quadrantOwners.Clone();
        }
    }
}
