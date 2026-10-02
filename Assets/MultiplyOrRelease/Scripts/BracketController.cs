using System;
using System.Collections;
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

        void OnEnable()
        {
            if (!Application.isPlaying && config != null) Rebuild();
            else rebuildRequested = true;
        }
        void OnValidate() { rebuildRequested = true; }
        void OnDisable() { Cleanup(); }
        void OnDestroy() { Cleanup(); }
        void Update()
        {
            if (rebuildRequested) { rebuildRequested = false; Rebuild(); }
        }
        public void Rebuild()
        {
            rebuildRequested = false; Cleanup();
            if (config == null) return;
            try { State = new BracketState(config.teams); }
            catch (ArgumentException ex) { Debug.LogError("Bracket: " + ex.Message, this); return; }
            view = new BracketView(transform, config, bracketCamera);
            view.Refresh(State); attempts = 0;
            Phase = BracketPhase.Ready;
            if (Application.isPlaying && simulation != null && config.simulation != null)
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
                if (config.flagMoveDelay > 0)
                    yield return new WaitForSecondsRealtime(config.flagMoveDelay);
                yield return view.AnimateMatch(ActiveMatch, Mathf.Max(.05f, config.flagMoveDuration));
                if (config.matchStartDelay > 0)
                    yield return new WaitForSecondsRealtime(config.matchStartDelay);
                matchConfig = State.CreateMatchConfig(config.simulation, ActiveMatch);
                matchConfig.randomSeed = unchecked(matchConfig.randomSeed + attempts++);
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
