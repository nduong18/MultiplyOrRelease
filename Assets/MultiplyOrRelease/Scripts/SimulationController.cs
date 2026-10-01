using UnityEngine;

namespace MultiplyOrRelease
{
    [ExecuteAlways]
    public sealed class SimulationController : MonoBehaviour
    {
        public SimulationConfig config;
        public Camera simulationCamera;
        [SerializeField, HideInInspector] int restartIndex;
        public SimulationModel Model { get; private set; }
        public bool Paused { get; private set; }
        public float Speed { get; private set; } = 1;
        public int CurrentSeed { get; private set; }
        public string PerformanceMessage { get; private set; } = "";
        SimulationConfig sessionConfig;
        SimulationView view;
        SimulationHud hud;
        float accumulator;
        bool rebuildRequested;

        void OnEnable() { rebuildRequested = true; }
        void OnValidate() { rebuildRequested = true; }
        void OnDisable() { Cleanup(); }
        void OnDestroy() { Cleanup(); }
        void Update()
        {
            if (rebuildRequested) { rebuildRequested = false; Rebuild(); }
            if (Model == null) return;
            FitCamera();
            if (!Application.isPlaying) return;
            if (!Paused && Model.phase != MatchPhase.Ready && Model.phase != MatchPhase.Finished)
            {
                accumulator += Mathf.Min(Time.unscaledDeltaTime, .25f) * Speed;
                float tick = 1f / sessionConfig.ticksPerSecond;
                int count = 0;
                while (accumulator >= tick && count < sessionConfig.maxTicksPerFrame)
                {
                    Model.Tick(tick); accumulator -= tick; count++;
                }
                // Limit debt after a long editor stall. Simulation remains stable;
                // the Inspector reports that it is behind the requested speed.
                bool behind = accumulator > tick * sessionConfig.maxTicksPerFrame;
                if (behind) accumulator = tick * sessionConfig.maxTicksPerFrame;
                PerformanceMessage = behind ? "CPU LIMIT · simulation slowed" : Model.shots.Count >= sessionConfig.projectile.maxActive ? "SHOT LIMIT · volleys queued" : "";
            }
            view.SetClock(Speed, Paused || Model.phase == MatchPhase.Finished);
            view.Render(); hud?.Render();
        }
        public void Rebuild()
        {
            Cleanup();
            if (config == null) return;
            if (simulationCamera == null) simulationCamera = GetComponentInChildren<Camera>();
            if (simulationCamera == null) simulationCamera = Camera.main;
            sessionConfig = Instantiate(config); sessionConfig.name = config.name + " (session)";
            sessionConfig.hideFlags = HideFlags.HideAndDontSave;
            sessionConfig.Validate();
            if (sessionConfig.presentation.circleSprite == null || sessionConfig.presentation.squareSprite == null ||
                sessionConfig.presentation.font == null || sessionConfig.presentation.spriteMaterial == null || sessionConfig.presentation.trailMaterial == null)
            {
                Debug.LogError("Multiply or Release: presentation assets are missing. Use Tools > Multiply or Release > Create or Open Simulation.", this);
                return;
            }
            CurrentSeed = sessionConfig.randomSeed + restartIndex;
            Model = new SimulationModel(sessionConfig, CurrentSeed);
            Speed = sessionConfig.simulationSpeed; Paused = false; accumulator = 0; PerformanceMessage = "";
            view = new SimulationView(transform, Model, !Application.isPlaying);
            if (sessionConfig.presentation.showHud) hud = new SimulationHud(transform, this, sessionConfig);
            // Transient meshes/atlases and preview objects regenerate on load. The
            // scene saves only the camera, controller, and persistent config asset.
            // Runtime objects already disappear when Play mode exits. DontSave
            // flags can detach UI behaviours from editor-driven play updates, so
            // reserve them for edit-mode preview objects only.
            if (!Application.isPlaying)
                foreach (Transform child in transform)
                    if (child.name == "Generated Preview" || child.name == "Simulation HUD") MarkTransient(child);
            FitCamera();
            if (Application.isPlaying && sessionConfig.autoStart) Model.Start();
            hud?.Render();
        }
        static void MarkTransient(Transform item)
        {
            item.gameObject.hideFlags = HideFlags.DontSave;
            foreach (Transform child in item) MarkTransient(child);
        }
        void Cleanup()
        {
            view?.Dispose(); view = null; hud?.Dispose(); hud = null; Model = null;
            if (sessionConfig != null)
            {
                if (Application.isPlaying) Destroy(sessionConfig); else DestroyImmediate(sessionConfig);
                sessionConfig = null;
            }
        }
        void FitCamera()
        {
            if (simulationCamera == null || sessionConfig == null) return;
            var c = sessionConfig;
            simulationCamera.backgroundColor = c.presentation.backgroundColor;
            if (!c.presentation.autoFrameCamera) return;
            bool arenaOnly = c.presentation.cameraFocus == CameraFocus.TerritoryGrid;
            float frame = c.presentation.frameThickness * 2;
            Vector2 outerSize = arenaOnly ? Vector2.one * (c.board.size + frame) : c.SimulationSize;
            float width = outerSize.x;
            float height = outerSize.y;
            float padding = Mathf.Max(0, c.presentation.cameraPadding) * 2;
            width += padding; height += padding;
            float usableHeight = c.presentation.showHud ? .78f : 1;
            float size = Mathf.Max(height * .5f, width / Mathf.Max(.1f, simulationCamera.aspect) * .5f) / usableHeight;
            simulationCamera.orthographic = true; simulationCamera.orthographicSize = size;
            simulationCamera.transform.position = new Vector3(c.presentation.cameraOffset.x, c.presentation.cameraOffset.y, -10);
        }
        public void TogglePause()
        {
            if (Model == null) return;
            if (Model.phase == MatchPhase.Ready) { Model.Start(); Paused = false; }
            else if (Model.phase != MatchPhase.Finished) Paused = !Paused;
        }
        public void Step()
        {
            if (Model == null || Model.phase == MatchPhase.Finished) return;
            if (Model.phase == MatchPhase.Ready) Model.Start();
            Paused = true; Model.Tick(1f / sessionConfig.ticksPerSecond); view.Render(); hud?.Render();
        }
        public void RestartSameSeed() { Rebuild(); }
        public void RestartNewSeed() { restartIndex++; Rebuild(); }
        public void CycleSpeed()
        {
            float[] speeds = { .5f, 1, 2, 4, 8 };
            for (int i = 0; i < speeds.Length; i++) if (Mathf.Abs(Speed - speeds[i]) < .01f) { Speed = speeds[(i + 1) % speeds.Length]; return; }
            Speed = 1;
        }
        public void SetSpeed(float value) { Speed = Mathf.Clamp(value, .1f, 8); }
        public void SetGridStyle(TerritoryStyle style)
        {
            if (sessionConfig == null || sessionConfig.board.style == style) return;
            sessionConfig.board.style = style;
            view.Render(true);
        }
        public void SetTrailsVisible(bool visible)
        {
            if (sessionConfig == null) return;
            sessionConfig.presentation.showTrails = visible;
            view.Render();
        }
        public void ToggleGridStyle()
        {
            if (sessionConfig == null) return;
            sessionConfig.board.style = sessionConfig.board.style == TerritoryStyle.Color ? TerritoryStyle.Flag : TerritoryStyle.Color;
            view.Render(true);
        }
        public void ToggleTrails() { if (sessionConfig != null) sessionConfig.presentation.showTrails = !sessionConfig.presentation.showTrails; }
        public TerritoryStyle GridStyle => sessionConfig != null ? sessionConfig.board.style : TerritoryStyle.Color;
        public bool TrailsVisible => sessionConfig != null && sessionConfig.presentation.showTrails;
        public string Diagnostics()
        {
            if (Model == null) return "No model";
            string s = "phase=" + Model.phase + " elapsed=" + Model.elapsed.ToString("0.00") + " alive=" + Model.AliveCount +
                " activeShots=" + Model.shots.Count + " fired=" + Model.totalFired + " seed=" + CurrentSeed + " winner=" + Model.winner;
            for (int t = 0; t < 4; t++)
            {
                var team = Model.teams[t]; int cycles = 0;
                foreach (var b in team.balls) cycles += b.cycles;
                s += "\n" + config.teams[t].name + ": ammo=" + team.ammo + " queued=" + team.queued + " fired=" + team.fired +
                    " ×2=" + team.multiplies + " releases=" + team.releases + " cells=" + Model.territoryCounts[t] + " cycles=" + cycles + " alive=" + team.alive;
            }
            return s;
        }
    }
}
