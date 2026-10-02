using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MultiplyOrRelease
{
    public enum BracketPhase { Ready, PreparingMatch, PlayingMatch, ShowingResult, Champion }

    [ExecuteAlways]
    public sealed class BracketController : MonoBehaviour
    {
        public BracketConfig config;
        public Camera bracketCamera;
        public SimulationController simulation;
        public BracketState State { get; private set; }
        public int ActiveMatch { get; private set; } = -1;
        public BracketPhase Phase { get; private set; }
        BracketView view;
        SimulationConfig matchConfig;
        SimulationCelebration championCelebration;
        bool rebuildRequested;
        int attempts;
        readonly HashSet<int> usedRandomSeeds = new HashSet<int>();
        BracketConfig builtConfig;
        TeamPreset[] configuredTeams;

        void OnEnable()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update -= UpdateEditorPreview;
            UnityEditor.EditorApplication.update += UpdateEditorPreview;
#endif
            if (!Application.isPlaying && config != null) Rebuild();
            else rebuildRequested = true;
        }
        void OnValidate() { rebuildRequested = true; }
        void OnDisable()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update -= UpdateEditorPreview;
#endif
            Cleanup();
        }
        void OnDestroy() { Cleanup(); }
#if UNITY_EDITOR
        void UpdateEditorPreview()
        {
            // Asset Inspector changes do not always tick ExecuteAlways.Update.
            if (!Application.isPlaying && !UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode &&
                !UnityEditor.EditorApplication.isCompiling && !UnityEditor.EditorApplication.isUpdating)
                Update();
        }
#endif
        void Update()
        {
            if (rebuildRequested || RosterChanged()) { rebuildRequested = false; Rebuild(); }
        }
        bool RosterChanged()
        {
            if (config != builtConfig) return true;
            var teams = config != null ? config.teams : null;
            if (teams == null || configuredTeams == null) return teams != configuredTeams;
            if (teams.Length != configuredTeams.Length) return true;
            for (int i = 0; i < teams.Length; i++) if (teams[i] != configuredTeams[i]) return true;
            return false;
        }
        public void Rebuild()
        {
            rebuildRequested = false; Cleanup();
            builtConfig = config;
            configuredTeams = config != null && config.teams != null ? (TeamPreset[])config.teams.Clone() : null;
            if (config == null) return;
            State = new BracketState(config.teams);
            view = new BracketView(transform, config, bracketCamera);
            view.Refresh(State); attempts = 0;
            Phase = BracketPhase.Ready;
            if (Application.isPlaying && State.IsReady && simulation != null && config.simulation != null)
                StartCoroutine(RunTournament());
        }
        IEnumerator RunTournament()
        {
            yield return null;
            while (State.NextMatch >= 0)
            {
                ActiveMatch = State.NextMatch;
                Phase = BracketPhase.PreparingMatch;
                view.ShowBracket(); view.Refresh(State);
                float moveDelay = attempts == 0 ? config.initialFlagMoveDelay : config.flagMoveDelay;
                if (moveDelay > 0)
                    yield return new WaitForSecondsRealtime(moveDelay);
                yield return view.AnimateMatch(ActiveMatch, Mathf.Max(.05f, config.flagMoveDuration));
                if (config.matchStartDelay > 0)
                    yield return new WaitForSecondsRealtime(config.matchStartDelay);
                matchConfig = State.CreateMatchConfig(config.simulation, ActiveMatch);
                matchConfig.randomSeed = NextMatchSeed(matchConfig.randomSeed);
                simulation.config = matchConfig;
                view.ShowMatch(); simulation.gameObject.SetActive(true);
                while (simulation.Model == null) yield return null;
                Phase = BracketPhase.PlayingMatch;
                while (simulation.Model.phase != MatchPhase.Finished) yield return null;
                if (matchConfig.celebration.enableVictoryCard)
                    while (!simulation.ResultCardVisible) yield return null;
                Phase = BracketPhase.ShowingResult;
                // Count from actual card visibility, not from the winning hit.
                if (config.winnerCardHoldDuration > 0)
                    yield return new WaitForSecondsRealtime(config.winnerCardHoldDuration);
                int winner = simulation.Model.winner;
                if (winner >= 0) State.RecordWinner(ActiveMatch, winner);
                EndMatch();
                view.ShowBracket(); view.Refresh(State);
                yield return null;
                // Draws replay the same group with a fresh seed.
            }
            Phase = BracketPhase.Champion;
            var host = new GameObject("Bracket Champion Celebration");
            host.transform.SetParent(transform, false);
            championCelebration = host.AddComponent<SimulationCelebration>();
            championCelebration.CelebrateChampion(bracketCamera, config.simulation);
        }
        int NextMatchSeed(int configuredSeed)
        {
            int attempt = attempts++;
            if (!config.randomizeMatchSeed) return unchecked(configuredSeed + attempt);
            int seed;
            // Keep seeds distinct across matches, draw replays and tournament rebuilds.
            // A new GUID also avoids repeating a sequence on the next Play session.
            do { seed = Guid.NewGuid().GetHashCode() & int.MaxValue; }
            while (!usedRandomSeeds.Add(seed));
            return seed;
        }
        void EndMatch()
        {
            if (simulation != null)
            {
                simulation.gameObject.SetActive(false);
                simulation.config = config != null ? config.simulation : null;
            }
            if (matchConfig != null)
            {
                if (Application.isPlaying) Destroy(matchConfig); else DestroyImmediate(matchConfig);
                matchConfig = null;
            }
            ActiveMatch = -1;
        }
        void Cleanup()
        {
            StopAllCoroutines();
            if (championCelebration != null)
            {
                championCelebration.Clear(); championCelebration.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(championCelebration.gameObject); else DestroyImmediate(championCelebration.gameObject);
                championCelebration = null;
            }
            EndMatch(); view?.Dispose(); view = null; State = null; Phase = BracketPhase.Ready;
        }
    }
}
