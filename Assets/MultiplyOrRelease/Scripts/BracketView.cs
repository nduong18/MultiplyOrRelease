using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace MultiplyOrRelease
{
    public sealed class BracketView : IDisposable
    {
        readonly BracketConfig config;
        readonly GameObject root, board, background;
        readonly Image[] flags = new Image[16], winnerFlags = new Image[4];
        readonly RectTransform[] slots = new RectTransform[16], winners = new RectTransform[4];
        readonly Vector2[] slotPositions = new Vector2[16], winnerPositions = new Vector2[4];
        readonly GameObject[] numbers = new GameObject[16];
        readonly Text[] winnerLabels = new Text[4];
        readonly Image championFlag;
        readonly Text championLabel, championName;
        GameObject finalIntro;
        Font Font => config.font != null ? config.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        public BracketView(Transform parent, BracketConfig settings, Camera camera)
        {
            config = settings;
            root = new GameObject("Bracket Presentation", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            root.transform.SetParent(parent, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = camera != null ? RenderMode.ScreenSpaceCamera : RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera = camera; canvas.planeDistance = 1; canvas.sortingOrder = 200;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1440, 810);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            background = Image("Background", root.transform, Vector2.zero, Vector2.zero, config.panelColor).gameObject;
            var bg = background.GetComponent<RectTransform>();
            bg.anchorMin = Vector2.zero; bg.anchorMax = Vector2.one; bg.offsetMin = bg.offsetMax = Vector2.zero;
            board = Rect("Bracket Board", root.transform, Vector2.zero, new Vector2(1440, 810)).gameObject;
            for (int group = 0; group < 4; group++) BuildGroup(group);
            Line(new Vector2(-175, 200), new Vector2(-175, -200));
            Line(new Vector2(175, 200), new Vector2(175, -200));
            Line(new Vector2(-175, 0), new Vector2(175, 0));
            var cup = Image("Champion Cup", board.transform, new Vector2(0, config.championCupHeight), Vector2.one * config.championCupSize, Color.white);
            cup.sprite = config.championCupSprite;
            cup.preserveAspect = true;
            cup.enabled = cup.sprite != null;
            Label("Final Title", "FINAL (M5)", board.transform, new Vector2(0, 97), new Vector2(200, 28), 18, config.lineColor);
            var champion = Ring("Champion", board.transform, Vector2.zero, 132, config.accentColor);
            championFlag = Image("Champion Flag", champion, Vector2.zero, new Vector2(113, 113), Color.white);
            championLabel = Label("Champion Placeholder", "1", champion, Vector2.zero, new Vector2(110, 70), 44, config.accentColor);
            Label("Champion Title", "CHAMPION", board.transform, new Vector2(0, -100), new Vector2(310, 40), 29, config.accentColor);
            championName = Label("Champion Name", "", board.transform, new Vector2(0, -146), new Vector2(510, 38), 26, config.textColor);
            championName.resizeTextForBestFit = true;
            championName.resizeTextMinSize = 14; championName.resizeTextMaxSize = 26;
            championName.gameObject.SetActive(false);
            if (!Application.isPlaying) MarkTransient(root.transform);
        }
        static void MarkTransient(Transform node)
        {
            node.gameObject.hideFlags = HideFlags.DontSave;
            foreach (Transform child in node) MarkTransient(child);
        }
        void BuildGroup(int group)
        {
            float sign = group < 2 ? -1 : 1;
            float y = group % 2 == 0 ? 200 : -200;
            Line(new Vector2(sign * 500, y + 135), new Vector2(sign * 500, y - 135));
            Line(new Vector2(sign * 500, y), new Vector2(sign * 175, y));
            for (int i = 0; i < 4; i++)
            {
                int slot = group * 4 + i;
                float fy = y + 135 - i * 90;
                Line(new Vector2(sign * 640, fy), new Vector2(sign * 500, fy));
                slotPositions[slot] = new Vector2(sign * 640, fy);
                var number = Ring("Slot Number " + slot, board.transform, slotPositions[slot], config.flagSize + 4, config.lineColor);
                Label("Number", (i + 1).ToString(), number, Vector2.zero, Vector2.one * config.flagSize, 35, config.textColor);
                numbers[slot] = number.gameObject; numbers[slot].SetActive(false);
                slots[slot] = Ring("Team Slot " + slot, board.transform, slotPositions[slot], config.flagSize + 4, config.lineColor);
                flags[slot] = Image("Flag", slots[slot], Vector2.zero, Vector2.one * config.flagSize, Color.white);
            }
            winnerPositions[group] = new Vector2(sign * 325, y);
            winners[group] = Ring("Winner W" + (group + 1), board.transform, winnerPositions[group], 90, config.lineColor);
            winnerFlags[group] = Image("Winner Flag", winners[group], Vector2.zero, new Vector2(78, 78), Color.white);
            winnerLabels[group] = Label("Winner Placeholder", "W" + (group + 1), winners[group], Vector2.zero, new Vector2(82, 44), 27, config.textColor);
        }
        public void Refresh(BracketState state)
        {
            for (int i = 0; i < 16; i++)
            {
                flags[i].sprite = state.TeamAt(i).team.cannonSprite;
                flags[i].enabled = flags[i].sprite != null;
                slots[i].anchoredPosition = slotPositions[i]; numbers[i].SetActive(false);
            }
            for (int i = 0; i < 4; i++)
            {
                var winner = state.GroupWinner(i);
                winnerFlags[i].sprite = winner != null ? winner.team.cannonSprite : null;
                winnerFlags[i].enabled = winnerFlags[i].sprite != null;
                winnerLabels[i].enabled = winner == null;
                winners[i].anchoredPosition = winnerPositions[i];
            }
            championFlag.sprite = state.Champion != null ? state.Champion.team.cannonSprite : null;
            championFlag.enabled = championFlag.sprite != null;
            championLabel.enabled = !championFlag.enabled;
            championName.text = state.Champion != null ? state.Champion.team.name : "";
            championName.gameObject.SetActive(state.Champion != null);
        }
        public IEnumerator AnimateMatch(int match, float duration)
        {
            var moving = new RectTransform[4];
            var starts = new Vector2[4]; var ends = new Vector2[4];
            if (match < 4)
            {
                float sign = match < 2 ? -1 : 1;
                for (int i = 0; i < 4; i++)
                {
                    int slot = match * 4 + i;
                    moving[i] = slots[slot]; starts[i] = slotPositions[slot];
                    ends[i] = new Vector2(sign * 500, starts[i].y);
                    numbers[slot].SetActive(true); moving[i].SetAsLastSibling();
                }
            }
            else
            {
                finalIntro = Rect("Final Lineup", board.transform, Vector2.zero, new Vector2(1440, 810)).gameObject;
                Image("Dim", finalIntro.transform, Vector2.zero, new Vector2(1440, 810), new Color(0, 0, 0, .8f));
                Label("Final", "FINAL", finalIntro.transform, new Vector2(0, 190), new Vector2(360, 45), 32, config.accentColor);
                for (int i = 0; i < 4; i++)
                {
                    moving[i] = winners[i]; starts[i] = winnerPositions[i];
                    ends[i] = new Vector2(-240 + i * 160, 40);
                    var number = Ring("Final Number " + i, finalIntro.transform, new Vector2(ends[i].x, -80), 76, config.lineColor);
                    Label("Number", (i + 1).ToString(), number, Vector2.zero, new Vector2(70, 60), 35, config.textColor);
                    moving[i].SetAsLastSibling();
                }
            }
            float started = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - started < duration)
            {
                float t = Mathf.Clamp01((Time.realtimeSinceStartup - started) / duration);
                t = t * t * (3 - 2 * t);
                for (int i = 0; i < 4; i++) moving[i].anchoredPosition = Vector2.Lerp(starts[i], ends[i], t);
                yield return null;
            }
            for (int i = 0; i < 4; i++) moving[i].anchoredPosition = ends[i];
        }
        public void ShowMatch() { board.SetActive(false); background.SetActive(false); }
        public void ShowBracket()
        {
            if (finalIntro != null) { finalIntro.SetActive(false); UnityEngine.Object.Destroy(finalIntro); finalIntro = null; }
            board.SetActive(true); background.SetActive(true);
        }
        RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var node = new GameObject(name, typeof(RectTransform)); node.transform.SetParent(parent, false);
            var rect = node.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
        }
        Image Image(string name, Transform parent, Vector2 position, Vector2 size, Color color)
        {
            var image = Rect(name, parent, position, size).gameObject.AddComponent<Image>();
            image.color = color; image.raycastTarget = false; return image;
        }
        Text Label(string name, string value, Transform parent, Vector2 position, Vector2 size, int fontSize, Color color)
        {
            var text = Rect(name, parent, position, size).gameObject.AddComponent<Text>();
            text.font = Font; text.text = value; text.fontSize = fontSize; text.color = color;
            text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false; return text;
        }
        RectTransform Ring(string name, Transform parent, Vector2 position, float diameter, Color color)
        {
            var outer = Image(name, parent, position, Vector2.one * diameter, color); outer.sprite = config.circleSprite;
            var inner = Image("Fill", outer.transform, Vector2.zero, Vector2.one * (diameter - config.lineWidth * 2), config.panelColor);
            inner.sprite = config.circleSprite; return outer.rectTransform;
        }
        void Line(Vector2 a, Vector2 b)
        {
            var size = new Vector2(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y));
            if (size.x < 1) size.x = config.lineWidth; if (size.y < 1) size.y = config.lineWidth;
            Image("Connector", board.transform, (a + b) * .5f, size, config.lineColor);
        }
        public void Dispose()
        {
            if (root == null) return; root.SetActive(false);
            if (Application.isPlaying) UnityEngine.Object.Destroy(root); else UnityEngine.Object.DestroyImmediate(root);
        }
    }
}
