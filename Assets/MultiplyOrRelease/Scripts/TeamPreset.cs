using UnityEngine;

namespace MultiplyOrRelease
{
    [CreateAssetMenu(menuName = "Multiply or Release/Team Preset")]
    public sealed class TeamPreset : ScriptableObject
    {
        [Tooltip("Corner where this snapshot was saved. Loading into another corner rotates Aim Degrees by the corner difference, preserving the aim offset.")]
        [Range(0, 3)] public int savedSlot;
        public TeamSettings team = new TeamSettings { aimDegrees = -45 };

        public void CaptureFrom(SimulationConfig config, int slot)
        {
            TeamSetup.CheckConfig(config); TeamSetup.CheckSlot(slot);
            savedSlot = slot; team = config.teams[slot].Copy();
        }
        public void ApplyTo(SimulationConfig config, int slot, bool alignTerritories = true)
        {
            TeamSetup.CheckConfig(config); TeamSetup.CheckSlot(slot);
            config.teams[slot] = TeamSetup.ForSlot(team, savedSlot, slot);
            if (alignTerritories) TeamSetup.AlignStartingTerritories(config);
        }
    }
}
