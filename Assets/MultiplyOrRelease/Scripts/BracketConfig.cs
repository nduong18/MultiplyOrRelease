using UnityEngine;

namespace MultiplyOrRelease
{
    [CreateAssetMenu(menuName = "Multiply or Release/Bracket Config")]
    public sealed class BracketConfig : ScriptableObject
    {
        public SimulationConfig simulation;
        [Tooltip("Four groups of four teams: M1/M2 on the left, M3/M4 on the right.")]
        public TeamPreset[] teams = new TeamPreset[16];
        public TeamPreset[] teamCatalog = new TeamPreset[0];
        public Font font;
        public Sprite circleSprite;
        public Sprite championCupSprite;
        [Min(1)] public float championCupSize = 220;
        public float championCupHeight = 270;
        public Color background = new Color(.075f, .075f, .08f);
        public Color panelColor = new Color(.105f, .105f, .11f);
        public Color lineColor = new Color(.67f, .69f, .7f);
        public Color textColor = new Color(.91f, .92f, .93f);
        public Color accentColor = new Color(1, .8f, .23f);
        [Range(48, 90)] public float flagSize = 76;
        [Range(1, 6)] public float lineWidth = 3;
        [Header("Automatic Tournament")]
        [Tooltip("Generate a fresh seed for every match and draw replay. Disable to use Simulation Random Seed plus the match attempt index.")]
        public bool randomizeMatchSeed = true;
        [Tooltip("Real seconds to show the first bracket before the first match's flags begin moving.")]
        [Min(0)] public float initialFlagMoveDelay = 7;
        [Tooltip("Real seconds to show the bracket before the flags begin moving.")]
        [Min(0)] public float flagMoveDelay = 3;
        [Tooltip("Real seconds for the four flags to slide into their numbered positions.")]
        [Min(.05f)] public float flagMoveDuration = 1;
        [Tooltip("Real seconds to hold the numbered flags before gameplay starts.")]
        [Min(0)] public float matchStartDelay = 2;
        [Tooltip("Real seconds to show the winner card before returning to the bracket.")]
        [Min(0)] public float winnerCardHoldDuration = 5;
    }
}
