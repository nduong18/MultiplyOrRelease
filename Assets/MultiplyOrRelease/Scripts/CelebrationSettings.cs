using System;
using UnityEngine;

namespace MultiplyOrRelease
{
    // Layout/timing values from TerritoryBattle/Assets/Scenes/TerritoryGrid.unity.
    [Serializable] public sealed class CelebrationSettings
    {
        [Header("321 GO — TerritoryBattle")]
        public bool enableStartCountdown = true;
        [Min(0)] public float countdownStartDelay = 1;
        [Tooltip("Delay the 3/2/1/GO animation after the countdown voice starts.")]
        [Min(0)] public float countdownAnimationDelay = 1;
        [Min(.1f)] public float countdownStepDuration = 1;
        [Min(24)] public int countdownFontSize = 200;
        public Font countdownFont;
        [Min(.05f)] public float countdownPopDuration = .35f;
        [Range(0, 3)] public float countdownPopOvershoot = 1.70158f;
        public Color threeColor = new Color(.1f, .45f, 1);
        public Color twoColor = new Color(1, .15f, .15f);
        public Color oneColor = new Color(1, .85f, .1f);
        public Color goColor = new Color(.15f, .9f, .3f);

        [Header("Winner Card — TerritoryBattle")]
        [Tooltip("Show the original card when the simulation finishes; does not change the match's winner rules.")]
        public bool enableVictoryCard = true;
        [Tooltip("Real seconds from the match ending to the result card, winner sound and confetti. Does not delay locking the winner.")]
        [Min(0)] public float victoryCardDelay = 1;
        [Min(.05f)] public float victoryPopDuration = .45f;
        [Range(0, 3)] public float victoryPopOvershoot = 1.70158f;
        [Tooltip("Leave empty to reuse Countdown Font, then Presentation Font.")]
        public Font victoryFont;
        [Min(24)] public int victoryTitleFontSize = 110;
        [Min(18)] public int victoryStatusFontSize = 64;
        public Color victoryBackdropColor = Color.clear;
        public Color victoryCardColor = new Color(.08f, .08f, .1f, .92f);
        public Color victoryStatusColor = Color.white;
        [Min(0)] public float victoryCardWidth = 700;
        [Min(0)] public float victoryCardHeight = 600;
        [Min(0)] public float victoryFlagSize = 200;

        [Header("Confetti — TerritoryBattle")]
        public bool enableVictoryConfetti = true;
        [Min(1)] public int victoryConfettiBurstCount = 100;
        [Min(0)] public float victoryConfettiBurstInterval = .08f;
        [Min(0)] public float victoryConfettiLoopInterval = .1f;
        public int victoryConfettiSortingOrder = 10002;
        [Tooltip("Persistent shader references keep the copied particle shaders available in builds.")]
        public Shader particleShader;
        public Shader glowShader;

        [Header("Sounds — TerritoryBattle")]
        public bool enableSounds = true;
        [Range(0, 1)] public float masterVolume = 1;
        public AudioClip countdownClip;
        public AudioClip countdownGoClip;
        public AudioClip winnerClip;
        [Tooltip("Played when an enemy bullet destroys a cannon flag.")]
        public AudioClip explosionClip;
        [Tooltip("Played when a boost reaches its cannon and the reward is applied.")]
        public AudioClip collectClip;

        public void Validate()
        {
            countdownStartDelay = Mathf.Max(0, countdownStartDelay);
            countdownAnimationDelay = Mathf.Max(0, countdownAnimationDelay);
            countdownStepDuration = Mathf.Max(.1f, countdownStepDuration);
            countdownFontSize = Mathf.Max(24, countdownFontSize);
            countdownPopDuration = Mathf.Max(.05f, countdownPopDuration);
            victoryCardDelay = Mathf.Max(0, victoryCardDelay);
            victoryPopDuration = Mathf.Max(.05f, victoryPopDuration);
            victoryTitleFontSize = Mathf.Max(24, victoryTitleFontSize);
            victoryStatusFontSize = Mathf.Max(18, victoryStatusFontSize);
            victoryCardWidth = Mathf.Max(0, victoryCardWidth);
            victoryCardHeight = Mathf.Max(0, victoryCardHeight);
            victoryFlagSize = Mathf.Max(0, victoryFlagSize);
            victoryConfettiBurstCount = Mathf.Max(1, victoryConfettiBurstCount);
            victoryConfettiBurstInterval = Mathf.Max(0, victoryConfettiBurstInterval);
            victoryConfettiLoopInterval = Mathf.Max(0, victoryConfettiLoopInterval);
            masterVolume = Mathf.Clamp01(masterVolume);
        }
    }
}
