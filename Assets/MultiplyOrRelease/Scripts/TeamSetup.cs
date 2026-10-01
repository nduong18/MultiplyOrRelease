using System;
using UnityEngine;

namespace MultiplyOrRelease
{
    public static class TeamSetup
    {
        public static readonly string[] SlotNames = { "Upper Left", "Upper Right", "Lower Left", "Lower Right" };
        static readonly float[] InwardAngles = { -45, -135, 45, 135 };

        public static void CheckSlot(int slot)
        {
            if (slot < 0 || slot > 3) throw new ArgumentOutOfRangeException(nameof(slot));
        }
        public static void CheckConfig(SimulationConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (config.teams == null || config.teams.Length != 4)
                throw new InvalidOperationException("A lineup must contain exactly four teams.");
            foreach (var team in config.teams)
                if (team == null) throw new InvalidOperationException("All four team slots must be configured.");
        }
        public static TeamSettings ForSlot(TeamSettings team, int from, int to)
        {
            CheckSlot(from); CheckSlot(to);
            if (team == null) throw new ArgumentNullException(nameof(team));
            var copy = team.Copy();
            if (from != to)
                copy.aimDegrees = Mathf.DeltaAngle(0, copy.aimDegrees + Mathf.DeltaAngle(InwardAngles[from], InwardAngles[to]));
            return copy;
        }
        public static void AlignStartingTerritories(SimulationConfig config)
        {
            config.board.quadrantOwners = new[] { 0, 1, 2, 3 };
        }
        // Move, not swap: intervening teams shift to fill the vacated slot.
        public static void Move(SimulationConfig config, int from, int to, bool alignTerritories = true)
        {
            CheckConfig(config); CheckSlot(from); CheckSlot(to);
            if (from == to) return;
            var previous = (TeamSettings[])config.teams.Clone();
            for (int destination = 0; destination < 4; destination++)
            {
                int source = destination;
                if (destination == to) source = from;
                else if (from < to && destination >= from && destination < to) source = destination + 1;
                else if (from > to && destination > to && destination <= from) source = destination - 1;
                config.teams[destination] = ForSlot(previous[source], source, destination);
            }
            if (alignTerritories) AlignStartingTerritories(config);
        }
    }
}
