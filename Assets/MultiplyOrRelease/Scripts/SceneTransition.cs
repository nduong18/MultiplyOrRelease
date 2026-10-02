using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace MultiplyOrRelease
{
    public enum SceneTransitionStyle { None, Fade, WipeLeft, WipeRight, WipeUp, WipeDown, Curtain }

    // A screen-space cover keeps the switch hidden even when cameras or UI change.
    public sealed class SceneTransition : IDisposable
    {
        readonly GameObject root;
        readonly RectTransform panel, secondPanel;
        readonly Image image, secondImage;
        SceneTransitionStyle style;
        float halfDuration;

        public SceneTransition(Transform parent)
        {
            root = new GameObject("Scene Transition", typeof(RectTransform), typeof(Canvas));
            root.transform.SetParent(parent, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32760;
            image = Panel("Cover"); panel = image.rectTransform;
            secondImage = Panel("Second Cover"); secondPanel = secondImage.rectTransform;
            root.SetActive(false);
        }
        Image Panel(string name)
        {
            var node = new GameObject(name, typeof(RectTransform), typeof(Image));
            node.transform.SetParent(root.transform, false);
            var graphic = node.GetComponent<Image>();
            graphic.raycastTarget = false;
            return graphic;
        }
        public IEnumerator Cover(SceneTransitionStyle selectedStyle, float duration, Color color)
        {
            style = selectedStyle;
            halfDuration = Mathf.Max(0, duration) * .5f;
            if (style == SceneTransitionStyle.None || halfDuration == 0) yield break;
            color.a = 1; image.color = secondImage.color = color;
            secondPanel.gameObject.SetActive(style == SceneTransitionStyle.Curtain);
            Apply(0);
            root.SetActive(true);
            yield return Animate(0, 1);
        }
        public IEnumerator Reveal()
        {
            if (!root.activeSelf) yield break;
            yield return Animate(1, 0);
            root.SetActive(false);
        }
        IEnumerator Animate(float from, float to)
        {
            float started = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - started < halfDuration)
            {
                float t = Mathf.Clamp01((Time.realtimeSinceStartup - started) / halfDuration);
                Apply(Mathf.Lerp(from, to, t * t * (3 - 2 * t)));
                yield return null;
            }
            Apply(to);
        }
        void Apply(float amount)
        {
            SetRect(panel, Vector2.zero, Vector2.one);
            var color = image.color;
            color.a = style == SceneTransitionStyle.Fade ? amount : 1;
            image.color = color;
            switch (style)
            {
                case SceneTransitionStyle.WipeLeft:
                    SetRect(panel, new Vector2(1 - amount, 0), Vector2.one); break;
                case SceneTransitionStyle.WipeRight:
                    SetRect(panel, Vector2.zero, new Vector2(amount, 1)); break;
                case SceneTransitionStyle.WipeUp:
                    SetRect(panel, Vector2.zero, new Vector2(1, amount)); break;
                case SceneTransitionStyle.WipeDown:
                    SetRect(panel, new Vector2(0, 1 - amount), Vector2.one); break;
                case SceneTransitionStyle.Curtain:
                    SetRect(panel, Vector2.zero, new Vector2(amount * .5f, 1));
                    SetRect(secondPanel, new Vector2(1 - amount * .5f, 0), Vector2.one); break;
            }
        }
        static void SetRect(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
        public void Dispose()
        {
            if (root == null) return;
            root.SetActive(false);
            if (Application.isPlaying) UnityEngine.Object.Destroy(root); else UnityEngine.Object.DestroyImmediate(root);
        }
    }
}
