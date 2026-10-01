using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace MultiplyOrRelease
{
    // Countdown, card and fireworks ported from TerritoryBattle.
    // Only the match/team adapter and lifecycle differ from the original.
    public sealed class SimulationCelebration : MonoBehaviour
    {
        SimulationController controller;
        SimulationConfig config;
        CelebrationSettings settings;
        AudioSource sfxSource;
        Action onCountdownComplete;
        Coroutine countdownRoutine, countdownPopRoutine, victoryPopRoutine, victoryConfettiRoutine;
        GameObject countdownCanvasObject, victoryCanvasObject;
        Text countdownText;
        RectTransform countdownRect, victoryCardRect;
        SimulationCelebrationFirework victoryConfettiEffect;
        bool resultShown;
        public bool IsCountingDown { get; private set; }

        public void Initialize(SimulationController owner, SimulationConfig session)
        {
            controller = owner; config = session; settings = config.celebration;
            if (Application.isPlaying)
            {
                sfxSource = gameObject.AddComponent<AudioSource>();
                sfxSource.playOnAwake = false; sfxSource.loop = false;
                sfxSource.spatialBlend = 0; sfxSource.volume = settings.masterVolume;
            }
        }
        public void BeginCountdown(Action complete)
        {
            if (IsCountingDown) return;
            onCountdownComplete = complete; IsCountingDown = true;
            countdownRoutine = StartCoroutine(RunStartCountdown());
        }
        IEnumerator WaitCountdown(float seconds)
        {
            float elapsed = 0;
            while (elapsed < seconds)
            {
                if (!controller.Paused) elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }
        void BeginGameplay()
        {
            IsCountingDown = false; countdownRoutine = null;
            DestroyCountdownDisplay();
            var complete = onCountdownComplete; onCountdownComplete = null;
            complete?.Invoke();
        }
        public void SkipCountdown()
        {
            if (countdownRoutine != null) StopCoroutine(countdownRoutine);
            countdownRoutine = null; onCountdownComplete = null; IsCountingDown = false;
            if (sfxSource != null) sfxSource.Stop();
            DestroyCountdownDisplay();
        }
        public void SetCountdownPaused(bool paused)
        {
            if (!IsCountingDown || sfxSource == null) return;
            if (paused) sfxSource.Pause(); else sfxSource.UnPause();
        }
        public void PresentResult(SimulationModel model)
        {
            if (resultShown || model.phase != MatchPhase.Finished) return;
            resultShown = true;
            if (!settings.enableVictoryCard) return;
            ShowVictoryCard(model.winner >= 0 ? config.teams[model.winner] :
                new TeamSettings { name = "DRAW", territoryColor = config.presentation.textColor }, model.winner >= 0);
            if (model.winner < 0)
            {
                victoryStatusText.text = "DRAW";
                if (victoryConfettiRoutine != null) StopCoroutine(victoryConfettiRoutine);
                victoryConfettiRoutine = null;
                if (victoryConfettiEffect != null) victoryConfettiEffect.gameObject.SetActive(false);
            }
        }
        public void PreviewCountdown()
        {
            CreateCountdownDisplay(); countdownText.text = "3";
            countdownText.color = settings.threeColor; SetCountdownScale(1);
        }
        Font ResolveVictoryFont() => settings.victoryFont != null ? settings.victoryFont :
            settings.countdownFont != null ? settings.countdownFont : config.presentation.font;
        void PlayClip(AudioClip clip)
        {
            if (Application.isPlaying && settings.enableSounds && clip != null && sfxSource != null)
                sfxSource.PlayOneShot(clip);
        }
        void DestroyCountdownDisplay()
        {
            if (countdownPopRoutine != null) StopCoroutine(countdownPopRoutine);
            countdownPopRoutine = null;
            if (countdownCanvasObject != null)
            {
                countdownCanvasObject.SetActive(false);
                if (Application.isPlaying) Destroy(countdownCanvasObject); else DestroyImmediate(countdownCanvasObject);
            }
            countdownCanvasObject = null; countdownText = null; countdownRect = null;
        }
        void DestroyVictoryDisplay()
        {
            if (victoryConfettiRoutine != null) StopCoroutine(victoryConfettiRoutine);
            if (victoryPopRoutine != null) StopCoroutine(victoryPopRoutine);
            victoryConfettiRoutine = victoryPopRoutine = null;
            if (victoryConfettiEffect != null)
            {
                victoryConfettiEffect.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(victoryConfettiEffect.gameObject);
                else DestroyImmediate(victoryConfettiEffect.gameObject);
            }
            if (victoryCanvasObject != null)
            {
                victoryCanvasObject.SetActive(false);
                if (Application.isPlaying) Destroy(victoryCanvasObject); else DestroyImmediate(victoryCanvasObject);
            }
            victoryConfettiEffect = null; victoryCanvasObject = null; victoryCardRect = null;
            victoryTitleText = null; victoryStatusText = null; victoryFlagImage = victoryFlagFallback = null;
        }
        public void Clear()
        {
            StopAllCoroutines(); countdownRoutine = null; onCountdownComplete = null; IsCountingDown = false;
            countdownPopRoutine = victoryPopRoutine = victoryConfettiRoutine = null;
            if (sfxSource != null) sfxSource.Stop();
            DestroyCountdownDisplay(); DestroyVictoryDisplay();
        }
        void OnDisable() { Clear(); }
        void OnDestroy() { Clear(); }
        private IEnumerator RunStartCountdown()
        {
            CreateCountdownDisplay();
            if (countdownText != null)
            {
                countdownText.text = string.Empty;
                countdownText.color = new Color(1f, 1f, 1f, 0f);
            }
            SetCountdownScale(0f);
            if (settings.countdownStartDelay > 0f)
                yield return WaitCountdown(settings.countdownStartDelay);

            string[] countdownValues = { "3", "2", "1", "GO!" };
            Color[] countdownColors = { settings.threeColor, settings.twoColor, settings.oneColor, settings.goColor };
            for (int i = 0; i < countdownValues.Length; i++)
            {
                if (countdownText != null)
                {
                    countdownText.text = countdownValues[i];
                    countdownText.color = countdownColors[i];
                }
                PlayCountdownTick(i);
                PlayCountdownPop();
                yield return WaitCountdown(settings.countdownStepDuration);
            }

            BeginGameplay();
        }

        private void PlayCountdownPop()
        {
            if (countdownPopRoutine != null) StopCoroutine(countdownPopRoutine);
            countdownPopRoutine = StartCoroutine(RunCountdownPop());
        }

        private IEnumerator RunCountdownPop()
        {
            float duration = Mathf.Max(0.05f, settings.countdownPopDuration);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (controller.Paused) { yield return null; continue; }
                elapsed += Time.unscaledDeltaTime;
                SetCountdownScale(EaseOutBack(Mathf.Clamp01(elapsed / duration), settings.countdownPopOvershoot));
                yield return null;
            }

            SetCountdownScale(1f);
            countdownPopRoutine = null;
        }

        private void SetCountdownScale(float scale)
        {
            if (countdownRect == null) return;
            countdownRect.localScale = Vector3.one * Mathf.Max(0f, scale);
        }

        /// <summary>Overshoots past 1 by settings.countdownPopOvershoot before settling, giving the pop its bounce.</summary>
        private static float EaseOutBack(float t, float overshoot)
        {
            float c3 = overshoot + 1f;
            float p = t - 1f;
            return 1f + c3 * p * p * p + overshoot * p * p;
        }

        private Text victoryTitleText;
        private Text victoryStatusText;
        private Image victoryFlagImage;
        private Image victoryFlagFallback;

        public void ShowVictoryCard(TeamSettings team, bool celebrate = true)
        {
            CreateVictoryDisplay();
            Sprite flagSprite = team.cannonSprite;
            Color teamColor = team.territoryColor;

            if (victoryTitleText != null)
            {
                victoryTitleText.text = team.name;
                victoryTitleText.color = teamColor;
            }
            if (victoryStatusText != null) victoryStatusText.text = "WINNER!";
            if (victoryFlagImage != null && victoryFlagFallback != null)
            {
                if (flagSprite != null)
                {
                    victoryFlagImage.sprite = flagSprite;
                    victoryFlagImage.enabled = true;
                    victoryFlagFallback.gameObject.SetActive(false);
                }
                else
                {
                    victoryFlagImage.enabled = false;
                    victoryFlagFallback.color = teamColor;
                    victoryFlagFallback.gameObject.SetActive(true);
                }
            }

            if (celebrate) PlayWinner();
            if (victoryCardRect != null) victoryCardRect.localScale = Vector3.zero;
            if (Application.isPlaying) victoryPopRoutine = StartCoroutine(RunVictoryPop());
            else victoryCardRect.localScale = Vector3.one;
            if (Application.isPlaying && celebrate && settings.enableVictoryConfetti && victoryConfettiRoutine == null)
                victoryConfettiRoutine = StartCoroutine(RunVictoryConfetti());
        }

        private IEnumerator RunVictoryConfetti()
        {
            Camera displayCamera = controller.simulationCamera;
            if (displayCamera == null) yield break;

            // Dedicated host avoids mutating or deleting unrelated particle effects.
            victoryConfettiEffect = null;
            if (victoryConfettiEffect == null)
            {
                GameObject host = new GameObject("Victory Confetti");
                host.transform.SetParent(transform, false);
                victoryConfettiEffect = host.AddComponent<SimulationCelebrationFirework>();
            }
            victoryConfettiEffect.particleShader = settings.particleShader;
            victoryConfettiEffect.glowShader = settings.glowShader;
            victoryConfettiEffect.SetSortingOrder(settings.victoryConfettiSortingOrder);

            while (victoryCanvasObject != null)
            {
                for (int burstIndex = 0; burstIndex < settings.victoryConfettiBurstCount; burstIndex++)
                {
                    float horizontal = UnityEngine.Random.Range(0.04f, 0.96f);
                    float vertical = UnityEngine.Random.Range(0.1f, 0.95f);
                    float cameraDistance = Mathf.Abs(displayCamera.transform.position.z);
                    Vector3 worldPosition = displayCamera.ViewportToWorldPoint(
                        new Vector3(horizontal, vertical, cameraDistance));
                    // Keep particles in front of the victory card's Screen Space Camera canvas.
                    worldPosition.z = displayCamera.transform.position.z + 0.5f;
                    victoryConfettiEffect.BurstAt(worldPosition);
                    if (settings.victoryConfettiBurstInterval > 0f)
                        yield return new WaitForSecondsRealtime(settings.victoryConfettiBurstInterval);
                }

                if (settings.victoryConfettiLoopInterval > 0f)
                    yield return new WaitForSecondsRealtime(settings.victoryConfettiLoopInterval);
                else
                    yield return null;
            }

            victoryConfettiRoutine = null;
        }

        private IEnumerator RunVictoryPop()
        {
            float duration = Mathf.Max(0.05f, settings.victoryPopDuration);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float scale = EaseOutBack(Mathf.Clamp01(elapsed / duration), settings.victoryPopOvershoot);
                if (victoryCardRect != null) victoryCardRect.localScale = Vector3.one * scale;
                yield return null;
            }

            if (victoryCardRect != null) victoryCardRect.localScale = Vector3.one;
            victoryPopRoutine = null;
        }

        private void CreateVictoryDisplay()
        {
            if (victoryCanvasObject != null) return;

            victoryCanvasObject = new GameObject("Victory Card");
            victoryCanvasObject.transform.SetParent(transform, false);
            Canvas canvas = victoryCanvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = controller.simulationCamera;
            if (canvas.worldCamera == null) canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.planeDistance = 1f;
            canvas.sortingOrder = 10001;
            CanvasScaler scaler = victoryCanvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            // No GraphicRaycaster - card is purely informational and gameplay continues underneath.

            AddStretchedImage(victoryCanvasObject.transform, "Backdrop", settings.victoryBackdropColor);

            GameObject cardObject = new GameObject("Card");
            cardObject.transform.SetParent(victoryCanvasObject.transform, false);
            RectTransform cardRect = cardObject.AddComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.anchoredPosition = Vector2.zero;
            cardRect.sizeDelta = new Vector2(settings.victoryCardWidth, settings.victoryCardHeight);
            victoryCardRect = cardRect;
            Image cardImage = cardObject.AddComponent<Image>();
            cardImage.color = settings.victoryCardColor;

            victoryTitleText = AddVictoryText(cardObject.transform, "Team Name",
                new Vector2(0.5f, 0.8f), settings.victoryTitleFontSize, ResolveVictoryFont(), "USA");
            victoryTitleText.color = Color.white;

            victoryStatusText = AddVictoryText(cardObject.transform, "Status",
                new Vector2(0.5f, 0.6f), settings.victoryStatusFontSize, ResolveVictoryFont(), "WINS!");
            victoryStatusText.color = settings.victoryStatusColor;

            RectTransform flagRect = NewChildRect(cardObject.transform, "Flag",
                new Vector2(0.5f, 0.3f), new Vector2(settings.victoryFlagSize, settings.victoryFlagSize));
            victoryFlagImage = flagRect.gameObject.AddComponent<Image>();
            victoryFlagImage.preserveAspect = true;
            victoryFlagImage.raycastTarget = false;

            // Fallback circle when the team has no marble sprite assigned.
            GameObject fallbackObject = new GameObject("Flag Fallback");
            fallbackObject.transform.SetParent(flagRect, false);
            RectTransform fallbackRect = fallbackObject.AddComponent<RectTransform>();
            fallbackRect.anchorMin = Vector2.zero;
            fallbackRect.anchorMax = Vector2.one;
            fallbackRect.offsetMin = Vector2.zero;
            fallbackRect.offsetMax = Vector2.zero;
            victoryFlagFallback = fallbackObject.AddComponent<Image>();
            victoryFlagFallback.color = Color.white;
            victoryFlagFallback.sprite = config.presentation.circleSprite;
            victoryFlagFallback.raycastTarget = false;
        }

        private static Image AddStretchedImage(Transform parent, string name, Color color)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Image image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Text AddVictoryText(Transform parent, string name, Vector2 anchor, int fontSize, Font preferredFont, string content)
        {
            RectTransform rect = NewChildRect(parent, name, anchor, new Vector2(900f, fontSize * 1.6f));
            Text text = rect.gameObject.AddComponent<Text>();
            Font f = preferredFont != null ? preferredFont : ResolveBuiltinFont();
            if (f != null) text.font = f;
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.BoldAndItalic;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.text = content;
            text.raycastTarget = false;
            Outline outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(3f, -3f);
            return text;
        }

        private static RectTransform NewChildRect(Transform parent, string name, Vector2 anchor, Vector2 size)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
            return rect;
        }

        private void CreateCountdownDisplay()
        {
            if (countdownCanvasObject != null) return;


            countdownCanvasObject = new GameObject("Start Countdown");
            countdownCanvasObject.transform.SetParent(transform, false);
            Canvas canvas = countdownCanvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10000;
            CanvasScaler scaler = countdownCanvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            // Display-only: no buttons or raycaster.

            GameObject textObject = new GameObject("Countdown Text");
            textObject.transform.SetParent(countdownCanvasObject.transform, false);
            RectTransform rect = textObject.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(900f, 300f);
            countdownRect = rect;

            countdownText = textObject.AddComponent<Text>();
            countdownText.font = settings.countdownFont != null ? settings.countdownFont : ResolveBuiltinFont();
            countdownText.fontSize = settings.countdownFontSize;
            countdownText.fontStyle = FontStyle.Bold;
            countdownText.alignment = TextAnchor.MiddleCenter;
            countdownText.color = settings.threeColor;
            countdownText.horizontalOverflow = HorizontalWrapMode.Overflow;
            countdownText.verticalOverflow = VerticalWrapMode.Overflow;
            countdownText.raycastTarget = false;
            Outline outline = textObject.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(6f, -6f);
        }

        private static Font ResolveBuiltinFont()
        {
            Font font = null;
            try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch (System.Exception) { }
            if (font == null)
            {
                try { font = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch (System.Exception) { }
            }
            if (font == null) font = Font.CreateDynamicFontFromOSFont(new[] { "Arial", "Segoe UI" }, 128);
            return font;
        }

        public void PlayCountdownTick(int index)
        {
            // Neu co clip tach rieng cho GO, dung no o buoc cuoi.
            if (index == 3 && settings.countdownGoClip != null)
            {
                PlayClip(settings.countdownGoClip);
                return;
            }
            // Neu chi co 1 clip dai, chi phat o buoc dau tien de tranh chong clip.
            // Nguoi dung muon phat moi tick thi gan clip ngan vao settings.countdownClip.
            if (index == 0 && settings.countdownClip != null)
            {
                PlayClip(settings.countdownClip);
                return;
            }
            // Fallback: phat settings.countdownClip moi tick neu clip ngan.
            if (settings.countdownClip != null && settings.countdownGoClip == null)
            {
                // Heuristic: neu clip dai > 2s thi chi phat 1 lan, nguoc lai phat moi tick.
                if (settings.countdownClip.length <= 2.5f) PlayClip(settings.countdownClip);
                else if (index == 0) PlayClip(settings.countdownClip);
            }
        }

        public void PlayWinner()
        {
            AudioClip clip = settings.winnerClip;
            if (clip != null) PlayClip(clip);
        }


    }
}
