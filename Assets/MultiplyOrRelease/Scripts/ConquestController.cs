using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace MultiplyOrRelease
{
    [ExecuteAlways]
    public sealed class ConquestController : MonoBehaviour
    {
        public ConquestConfig config;
        public Camera simulationCamera;
        public ConquestModel Model { get; private set; }
        public bool Paused { get; private set; }
        public bool WinnerCardVisible => winnerCard != null && winnerCard.activeSelf;
        ConquestView view;
        ConquestConfig session;
        GameObject winnerCard;
        AudioSource sfxSource;
        float accumulator, resultElapsed;

        void OnEnable() { if (config != null) Rebuild(); }
        void OnDisable() => Clear();
        void Clear()
        {
            if (Model != null)
            {
                Model.CannonCaptured -= PlayCannonHitSfx;
                Model.PlinkoBoostCollected -= PlayBoostCollectSfx;
                Model.Won -= PlayWinnerSfx;
            }
            if (sfxSource != null) sfxSource.Stop();
            view?.Dispose(); view = null;
            if (winnerCard != null) { winnerCard.SetActive(false); DestroyObject(winnerCard); winnerCard = null; }
            if (session != null) { DestroyObject(session); session = null; }
        }
        [ContextMenu("Apply / Restart")]
        public void Rebuild()
        {
            Clear(); if (config == null) return; config.Validate();
            if (config.presentation.circleSprite == null || config.presentation.squareSprite == null ||
                config.presentation.spriteMaterial == null || config.presentation.trailMaterial == null || config.presentation.font == null)
            { Debug.LogError("ConquestConfig is missing its presentation assets. Use Create or Open 24 Country Conquest.", this); return; }
            // Only this controller's generated content is rebuilt, including old serialized previews.
            for (int i = transform.childCount - 1; i >= 0; i--)
                if (transform.GetChild(i).name == "Conquest Visuals" || transform.GetChild(i).name == "Conquest Winner Card")
                { var generated = transform.GetChild(i).gameObject; generated.SetActive(false); DestroyObject(generated); }
            session = Instantiate(config); session.hideFlags = HideFlags.HideAndDontSave;
            Model = new ConquestModel(session, config.randomSeed); view = new ConquestView(transform, session, Model);
            Model.CannonCaptured += PlayCannonHitSfx;
            Model.PlinkoBoostCollected += PlayBoostCollectSfx;
            Model.Won += PlayWinnerSfx;
            Paused = !config.autoStart; accumulator = resultElapsed = 0; FrameCamera();
        }
        void PlayCannonHitSfx(int slot, int previous, int next)
        {
            if (config.enableCannonHitSfx) PlaySfx(config.cannonHitClip, config.cannonHitVolume);
        }
        void PlayBoostCollectSfx(int boost, int slot)
        {
            if (config.enableBoostCollectSfx) PlaySfx(config.boostCollectClip, config.boostCollectVolume);
        }
        void PlayWinnerSfx(int owner)
        {
            if (config.enableWinnerSfx) PlaySfx(config.winnerClip, config.winnerVolume);
        }
        void PlaySfx(AudioClip clip, float volume)
        {
            if (!Application.isPlaying || clip == null) return;
            if (sfxSource == null)
            {
                sfxSource = gameObject.AddComponent<AudioSource>();
                sfxSource.hideFlags = HideFlags.DontSave;
                sfxSource.playOnAwake = false; sfxSource.loop = false;
                sfxSource.spatialBlend = 0;
            }
            sfxSource.clip = clip;
            sfxSource.PlayOneShot(clip, volume);
        }
        public void TogglePause() => Paused = !Paused;
        public void RestartNewSeed() { config.randomSeed++; Rebuild(); }
        public void Step()
        {
            if (Model == null || Model.Finished) return;
            Paused = true; float dt = 1f / config.ticksPerSecond;
            Model.Tick(dt); Model.AdvanceFiringFrame(); view.Render(dt, config.simulationSpeed, true);
        }
        void Update()
        {
            if (Model == null || view == null) return;
            session.ammoTextOffset = config.ammoTextOffset;
            FrameCamera();
            float advanced = 0;
            if (Application.isPlaying && !Paused && !Model.Finished)
            {
                float dt = 1f / session.ticksPerSecond;
                accumulator += Mathf.Min(Time.unscaledDeltaTime, .25f) * Mathf.Clamp(config.simulationSpeed, .1f, 8);
                int ticks = 0;
                while (accumulator >= dt && ticks < session.maxTicksPerFrame && !Model.Finished)
                { Model.Tick(dt); accumulator -= dt; advanced += dt; ticks++; }
                Model.AdvanceFiringFrame();
            }
            view.Render(advanced, config.simulationSpeed, !Application.isPlaying || Paused || Model.Finished);
            if (Application.isPlaying && Model.Finished && config.showWinnerCard && winnerCard == null)
            {
                resultElapsed += Time.unscaledDeltaTime;
                if (resultElapsed >= config.winnerCardDelay) ShowWinnerCard(Model.winner);
            }
        }
        void FrameCamera()
        {
            if (!config.presentation.autoFrameCamera) return;
            if (simulationCamera == null) simulationCamera = Camera.main; if (simulationCamera == null) return;
            var size = config.OuterSize;
            float center = (config.plinko.boardGap + config.plinko.width) * .5f;
            simulationCamera.orthographic = true;
            simulationCamera.orthographicSize = Mathf.Max(size.y * .5f, size.x / (2 * Mathf.Max(.1f, simulationCamera.aspect))) + config.presentation.cameraPadding;
            simulationCamera.transform.position = new Vector3(center + config.presentation.cameraOffset.x, config.presentation.cameraOffset.y, -10);
            simulationCamera.transform.rotation = Quaternion.identity; simulationCamera.clearFlags = CameraClearFlags.SolidColor;
            simulationCamera.backgroundColor = config.presentation.backgroundColor;
        }
        [ContextMenu("Preview Winner Card")]
        public void PreviewWinnerCard() { if (config != null) ShowWinnerCard(Model != null && Model.Finished ? Model.winner : 0); }
        public void ClearWinnerPreview()
        { if (winnerCard != null) { winnerCard.SetActive(false); DestroyObject(winnerCard); winnerCard = null; } }
        void ShowWinnerCard(int owner)
        {
            ClearWinnerPreview(); var team = config.countries[owner];
            winnerCard = new GameObject("Conquest Winner Card", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            winnerCard.hideFlags = HideFlags.DontSave;
            // Keep the overlay in the controller's scene, including when another additive scene is active.
            if (winnerCard.scene != gameObject.scene) SceneManager.MoveGameObjectToScene(winnerCard, gameObject.scene);
            winnerCard.transform.SetParent(transform, false);
            var canvas = winnerCard.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 1000;
            var scaler = winnerCard.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            var backdrop = Panel("Backdrop", winnerCard.transform, new Vector2(1920, 1080), new Color(0, 0, 0, .68f));
            backdrop.anchorMin = Vector2.zero; backdrop.anchorMax = Vector2.one; backdrop.sizeDelta = Vector2.zero;
            var card = Panel("Winner", winnerCard.transform, new Vector2(700, 590), config.winnerCardColor);
            var stripe = Panel("Country Accent", card, new Vector2(700, 12), team.territoryColor); stripe.anchoredPosition = new Vector2(0, 289);
            Label("Winner Title", config.winnerTitle, card, new Vector2(0, 210), 54, team.ammoTextColor);
            var flag = Panel("Winner Flag", card, Vector2.one * 190, Color.white); flag.anchoredPosition = new Vector2(0, 50);
            var image = flag.GetComponent<Image>(); image.sprite = team.cannonSprite != null ? team.cannonSprite : config.presentation.circleSprite;
            image.preserveAspect = true; image.color = team.cannonSprite != null ? team.cannonTint : team.territoryColor;
            Label("Country", team.name.ToUpperInvariant(), card, new Vector2(0, -115), 58, config.presentation.textColor);
            Label("Result", "ALL 24 CANNONS CAPTURED", card, new Vector2(0, -200), 26, config.presentation.secondaryTextColor);
        }
        static RectTransform Panel(string label, Transform parent, Vector2 size, Color color)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image)); var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f; rect.sizeDelta = size;
            go.GetComponent<Image>().color = color; go.GetComponent<Image>().raycastTarget = false; return rect;
        }
        void Label(string label, string text, Transform parent, Vector2 position, int fontSize, Color color)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Text)); var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
            rect.anchoredPosition = position; rect.sizeDelta = new Vector2(650, 100);
            var component = go.GetComponent<Text>(); component.font = config.winnerFont != null ? config.winnerFont : config.presentation.font;
            component.fontSize = fontSize; component.resizeTextForBestFit = true; component.resizeTextMinSize = 16; component.resizeTextMaxSize = fontSize;
            component.alignment = TextAnchor.MiddleCenter; component.text = text; component.color = color; component.raycastTarget = false;
        }
        static void DestroyObject(Object obj) { if (Application.isPlaying) Destroy(obj); else DestroyImmediate(obj); }
    }
}
