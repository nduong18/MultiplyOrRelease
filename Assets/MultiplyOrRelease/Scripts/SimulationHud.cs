using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace MultiplyOrRelease
{
    public sealed class SimulationHud : IDisposable
    {
        readonly SimulationController controller;
        readonly SimulationConfig config;
        readonly GameObject root;
        readonly Text matchStatus, statistics, resultTitle, resultSubtitle;
        readonly Text[] teamLabels = new Text[4];
        readonly Text pauseLabel, speedLabel, gridLabel, trailLabel;
        readonly GameObject resultPanel;
        GameObject createdEventSystem;
        public SimulationHud(Transform parent, SimulationController target, SimulationConfig c)
        {
            controller = target; config = c;
            root = new GameObject("Simulation HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(parent, false);
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 100;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            var header = Panel("Header", root.transform, c.presentation.panelColor);
            Layout(header, new Vector2(0, 1), new Vector2(1, 1), new Vector2(.5f, 1), Vector2.zero, new Vector2(0, 100));
            Label("Title", c.presentation.title, header, new Vector2(28, -28), new Vector2(370, 38), c.presentation.titleFontSize, c.presentation.textColor, TextAnchor.MiddleLeft, new Vector2(0, 1));
            Label("Subtitle", c.presentation.subtitle, header, new Vector2(28, -63), new Vector2(370, 26), 13, c.presentation.secondaryTextColor, TextAnchor.MiddleLeft, new Vector2(0, 1));
            for (int t = 0; t < 4; t++)
            {
                teamLabels[t] = Label("Team " + t, "", header, new Vector2(0, -45), new Vector2(200, 58), 17, c.teams[t].ammoTextColor, TextAnchor.MiddleCenter, new Vector2(.33f + t * .14f, 1));
            }
            matchStatus = Label("Match Status", "READY", header, new Vector2(-28, -30), new Vector2(210, 36), 18, c.presentation.textColor, TextAnchor.MiddleRight, new Vector2(1, 1));
            var footer = Panel("Controls", root.transform, c.presentation.panelColor);
            Layout(footer, Vector2.zero, new Vector2(1, 0), new Vector2(.5f, 0), Vector2.zero, new Vector2(0, 94));
            pauseLabel = Button("Pause", footer, 28, 142, controller.TogglePause);
            Button("Step", footer, 182, 94, controller.Step);
            Button("Restart", footer, 288, 120, controller.RestartSameSeed);
            Button("New seed", footer, 420, 134, controller.RestartNewSeed);
            speedLabel = Button("1× speed", footer, 566, 130, controller.CycleSpeed);
            gridLabel = Button("Color grid", footer, 708, 142, controller.ToggleGridStyle);
            trailLabel = Button("Trails on", footer, 862, 132, controller.ToggleTrails);
            statistics = Label("Stats", "", footer, new Vector2(-28, 48), new Vector2(800, 48), 15, c.presentation.secondaryTextColor, TextAnchor.MiddleRight, new Vector2(1, 0));
            var result = Panel("Result", root.transform, c.presentation.panelColor);
            Layout(result, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(680, 260));
            resultPanel = result.gameObject;
            resultTitle = Label("Winner", "", result, new Vector2(0, 52), new Vector2(640, 68), c.presentation.resultFontSize, c.presentation.textColor, TextAnchor.MiddleCenter, new Vector2(.5f, .5f));
            resultSubtitle = Label("Reason", "", result, new Vector2(0, -4), new Vector2(640, 42), 18, c.presentation.secondaryTextColor, TextAnchor.MiddleCenter, new Vector2(.5f, .5f));
            var again = new GameObject("Play Again", typeof(RectTransform)); again.transform.SetParent(result, false);
            Layout(again.GetComponent<RectTransform>(), new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(-88, -97), new Vector2(176, 50));
            ButtonInside(again.GetComponent<RectTransform>(), "New match", controller.RestartNewSeed);
            resultPanel.SetActive(false);
            if (Application.isPlaying && EventSystem.current == null)
            {
                createdEventSystem = new GameObject("Simulation Event System", typeof(EventSystem));
                createdEventSystem.transform.SetParent(root.transform, false);
#if ENABLE_INPUT_SYSTEM
                createdEventSystem.AddComponent<InputSystemUIInputModule>();
#else
                createdEventSystem.AddComponent<StandaloneInputModule>();
#endif
                // Register the module after both components exist. This also
                // handles a HUD recreated during an editor/domain reload.
                createdEventSystem.GetComponent<EventSystem>().UpdateModules();
            }
        }
        RectTransform Panel(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            return go.GetComponent<RectTransform>();
        }
        static void Layout(RectTransform rt, Vector2 min, Vector2 max, Vector2 pivot, Vector2 position, Vector2 size)
        { rt.anchorMin = min; rt.anchorMax = max; rt.pivot = pivot; rt.anchoredPosition = position; rt.sizeDelta = size; }
        Text Label(string name, string value, Transform parent, Vector2 pos, Vector2 size, int fontSize, Color color, TextAnchor alignment, Vector2 anchor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false);
            Layout(go.GetComponent<RectTransform>(), anchor, anchor, new Vector2(alignment == TextAnchor.MiddleLeft ? 0 : alignment == TextAnchor.MiddleRight ? 1 : .5f, .5f), pos, size);
            var text = go.GetComponent<Text>(); text.font = config.presentation.font; text.text = value; text.fontSize = fontSize;
            text.color = color; text.alignment = alignment; text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow; text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }
        Text Button(string title, Transform parent, float x, float width, UnityEngine.Events.UnityAction action)
        {
            var panel = Panel(title, parent, config.presentation.buttonColor);
            Layout(panel, Vector2.zero, Vector2.zero, new Vector2(0, 0), new Vector2(x, 23), new Vector2(width, 48));
            return ButtonInside(panel, title, action);
        }
        Text ButtonInside(RectTransform panel, string title, UnityEngine.Events.UnityAction action)
        {
            if (panel.GetComponent<Image>() == null) panel.gameObject.AddComponent<Image>().color = config.presentation.buttonColor;
            var button = panel.gameObject.AddComponent<Button>(); button.onClick.AddListener(action);
            var colors = button.colors; colors.normalColor = Color.white; colors.highlightedColor = config.presentation.buttonHighlightColor; colors.pressedColor = config.presentation.buttonPressedColor; button.colors = colors;
            return Label("Label", title, panel, Vector2.zero, panel.sizeDelta, config.presentation.hudFontSize, config.presentation.textColor, TextAnchor.MiddleCenter, new Vector2(.5f, .5f));
        }
        public void Render()
        {
            var m = controller.Model; if (m == null) return;
            for (int t = 0; t < 4; t++)
            {
                var team = m.teams[t];
                float percent = m.territoryCounts[t] * 100f / m.owners.Length;
                teamLabels[t].text = config.teams[t].name.ToUpperInvariant() + "\n" + (team.alive ? percent.ToString("0.0") + "%" : "ELIMINATED");
                teamLabels[t].color = team.alive ? config.teams[t].ammoTextColor : config.presentation.secondaryTextColor;
            }
            string time = TimeSpan.FromSeconds(m.elapsed).ToString(@"mm\:ss");
            matchStatus.text = (controller.Paused ? "PAUSED" : m.phase.ToString().ToUpperInvariant()) + "  " + time;
            pauseLabel.text = m.phase == MatchPhase.Ready ? "Start" : controller.Paused ? "Resume" : "Pause";
            speedLabel.text = controller.Speed.ToString("0.#") + "× speed";
            gridLabel.text = controller.GridStyle == TerritoryStyle.Flag ? "Flag grid" : "Color grid";
            trailLabel.text = controller.TrailsVisible ? "Trails on" : "Trails off";
            statistics.text = "SEED " + controller.CurrentSeed + "    ·    " + m.shots.Count.ToString("N0") + " ACTIVE    ·    " + SimulationView.ShortNumber(m.totalFired) + " FIRED\n" +
                (m.phase == MatchPhase.Settling ? "Resolving airborne projectiles…" : controller.PerformanceMessage);
            resultPanel.SetActive(m.phase == MatchPhase.Finished);
            if (m.phase == MatchPhase.Finished)
            {
                resultTitle.text = m.winner < 0 ? "DRAW" : config.teams[m.winner].name.ToUpperInvariant() + " WINS";
                resultTitle.color = m.winner < 0 ? config.presentation.textColor : config.teams[m.winner].ammoTextColor;
                resultSubtitle.text = m.resultReason + "  ·  " + time;
            }
        }
        public void Dispose()
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(root); else UnityEngine.Object.DestroyImmediate(root);
        }
    }
}
