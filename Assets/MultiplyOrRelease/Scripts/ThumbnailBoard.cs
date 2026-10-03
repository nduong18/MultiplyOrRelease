using System;
using UnityEngine;

namespace MultiplyOrRelease
{
    public enum ThumbnailFlagFit { Stretch, Contain, Cover }

    /// <summary>A static flag mosaic, independent of simulation and tournament state.</summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(SpriteRenderer))]
    public sealed class ThumbnailBoard : MonoBehaviour
    {
        [Header("Teams (left to right, top to bottom)")]
        [Tooltip("Drag any number of TeamPreset assets here. Reorder to change the thumbnail.")]
        public TeamPreset[] teams = Array.Empty<TeamPreset>();
        [Header("Layout")]
        [Tooltip("0 chooses columns automatically. For the reference layout use 6 columns and 18 teams.")]
        [Min(0)] public int columns = 6;
        public bool centerLastRow = true;
        [Tooltip("Outer margin as a fraction of image height.")]
        [Range(0, .2f)] public float margin;
        [Tooltip("Space between team panels as a fraction of image height.")]
        [Range(0, .1f)] public float panelGap;
        [Tooltip("Outer board corner radius as a fraction of image height.")]
        [Range(0, .2f)] public float cornerRadius;
        public Color backgroundColor = new Color(.055f, .055f, .055f, 1);
        public Color emptyColor = new Color(.1f, .1f, .1f, 1);
        [Header("Flag grid")]
        [Min(1)] public int cellsAcross = 28;
        [Tooltip("0 chooses rows to keep grid cells square in each panel.")]
        [Min(0)] public int cellsDown = 28;
        [Range(0, .4f)] public float gridLineThickness;
        public Color gridColor = new Color(.025f, .025f, .025f, 1);
        [Range(0, .25f)] public float checkerVariation = .08f;
        public ThumbnailFlagFit flagFit = ThumbnailFlagFit.Stretch;
        [Header("Image")]
        [Range(320, 4096)] public int imageWidth = 1920;
        [Range(180, 4096)] public int imageHeight = 1080;
        public Camera thumbnailCamera;
        public Material spriteMaterial;

        const float WorldWidth = 16;
        Texture2D previewTexture;
        Sprite previewSprite;
        bool rebuildPending;

        public Vector2Int Layout => CalculateLayout(teams == null ? 0 : teams.Length, columns,
            (float)Mathf.Max(1, imageWidth) / Mathf.Max(1, imageHeight));

        public static Vector2Int CalculateLayout(int count, int requestedColumns, float aspect)
        {
            if (count <= 0) return Vector2Int.zero;
            int cols = Mathf.Clamp(requestedColumns, 1, count);
            if (requestedColumns <= 0)
            {
                float best = float.PositiveInfinity;
                // One candidate per possible row count would also be valid. No team-count cap.
                for (int c = 1; c <= count; c++)
                {
                    int r = 1 + (count - 1) / c;
                    float score = Mathf.Abs(Mathf.Log(Mathf.Max(.001f, aspect) * r / c))
                        + (float)((long)c * r - count) / count;
                    if (score < best) { best = score; cols = c; }
                }
            }
            return new Vector2Int(cols, 1 + (count - 1) / cols);
        }

        void OnEnable()
        {
            rebuildPending = true;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update -= UpdateEditorPreview;
            UnityEditor.EditorApplication.update += UpdateEditorPreview;
#endif
        }
        void OnValidate() { rebuildPending = true; }
        void Update()
        {
            if (rebuildPending) Rebuild();
            FrameCamera();
        }
        void OnDisable()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update -= UpdateEditorPreview;
#endif
            ClearPreview();
        }
#if UNITY_EDITOR
        void UpdateEditorPreview()
        {
            // ExecuteAlways.Update does not consistently tick while Unity is unfocused.
            if (!Application.isPlaying && !UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode &&
                !UnityEditor.EditorApplication.isCompiling && !UnityEditor.EditorApplication.isUpdating)
                Update();
        }
#endif

        [ContextMenu("Rebuild Thumbnail")]
        public void Rebuild()
        {
            rebuildPending = false;
            ClearPreview();
            previewTexture = CreateImage();
            previewSprite = Sprite.Create(previewTexture, new Rect(0, 0, previewTexture.width, previewTexture.height),
                Vector2.one * .5f, previewTexture.width / WorldWidth, 0, SpriteMeshType.FullRect);
            previewSprite.hideFlags = HideFlags.HideAndDontSave;
            // Keep the renderer in the scene. Temporary editor GameObjects can be
            // excluded from camera culling after domain reload in Unity 6.
            var renderer = GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = previewSprite;
            if (spriteMaterial != null) renderer.sharedMaterial = spriteMaterial;
            FrameCamera();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
            UnityEditor.SceneView.RepaintAll();
#endif
        }

        void FrameCamera()
        {
            if (thumbnailCamera == null) return;
            float height = WorldWidth * Mathf.Max(180, imageHeight) / Mathf.Max(320, imageWidth);
            thumbnailCamera.orthographic = true;
            thumbnailCamera.orthographicSize = Mathf.Max(height * .5f, WorldWidth / (2 * Mathf.Max(.01f, thumbnailCamera.aspect)));
            thumbnailCamera.clearFlags = CameraClearFlags.SolidColor;
            thumbnailCamera.backgroundColor = backgroundColor;
            thumbnailCamera.transform.position = transform.position + new Vector3(0, 0, -10);
            thumbnailCamera.transform.rotation = Quaternion.identity;
        }

        /// <summary>Caller owns this texture. PNG export uses the same pixels as the preview.</summary>
        public Texture2D CreateImage()
        {
            int width = Mathf.Clamp(imageWidth, 320, 4096), height = Mathf.Clamp(imageHeight, 180, 4096);
            var pixels = new Color32[width * height];
            Color32 background = backgroundColor;
            for (int p = 0; p < pixels.Length; p++) pixels[p] = background;
            var layout = CalculateLayout(teams == null ? 0 : teams.Length, columns, (float)width / height);
            if (layout.x > 0)
            {
                float inset = Mathf.Clamp(margin, 0, .2f) * height;
                float gap = Mathf.Clamp(panelGap, 0, .1f) * height;
                var board = new Rect(inset, inset, width - 2 * inset, height - 2 * inset);
                // Even very large lineups still fit the board, without negative panel dimensions.
                gap = Mathf.Min(gap, Mathf.Min(board.width / layout.x, board.height / layout.y) * .8f);
                float tileWidth = (board.width - gap * (layout.x - 1)) / layout.x;
                float tileHeight = (board.height - gap * (layout.y - 1)) / layout.y;
                float radius = Mathf.Min(Mathf.Clamp(cornerRadius, 0, .2f) * height, Mathf.Min(board.width, board.height) * .5f);
                int across = Mathf.Clamp(cellsAcross, 1, 512);
                int down = cellsDown > 0 ? Mathf.Clamp(cellsDown, 1, 512) : Mathf.Clamp(Mathf.RoundToInt(across * tileHeight / tileWidth), 1, 512);
                for (int i = 0; i < teams.Length; i++)
                {
                    int row = i / layout.x, col = i % layout.x;
                    int inRow = Mathf.Min(layout.x, teams.Length - row * layout.x);
                    float shift = centerLastRow ? (layout.x - inRow) * (tileWidth + gap) * .5f : 0;
                    var tile = new Rect(board.x + col * (tileWidth + gap) + shift,
                        board.yMax - (row + 1) * tileHeight - row * gap, tileWidth, tileHeight);
                    var team = teams[i] != null ? teams[i].team : null;
                    Color fallback = team != null ? team.territoryColor : emptyColor;
                    int xMin = Mathf.Max(0, Mathf.CeilToInt(tile.xMin - .5f)), xMax = Mathf.Min(width, Mathf.CeilToInt(tile.xMax - .5f));
                    int yMin = Mathf.Max(0, Mathf.CeilToInt(tile.yMin - .5f)), yMax = Mathf.Min(height, Mathf.CeilToInt(tile.yMax - .5f));
                    int sampleWidth = xMax - xMin, sampleHeight = yMax - yMin;
                    if (sampleWidth <= 0 || sampleHeight <= 0) continue;
                    // Sample the full flag continuously. Grid cells crop the image just as the
                    // simulation mesh does; they never replace it with one color per cell.
                    var colors = SampleFlag(team != null ? team.territoryFlag : null, fallback, sampleWidth, sampleHeight, tileWidth / tileHeight);
                    for (int y = yMin; y < yMax; y++) for (int x = xMin; x < xMax; x++)
                    {
                        if (!InsideRoundedBoard(x + .5f, y + .5f, board, radius)) continue;
                        float gx = (x + .5f - tile.x) / tileWidth * across, gy = (y + .5f - tile.y) / tileHeight * down;
                        int cx = Mathf.Clamp((int)gx, 0, across - 1), cy = Mathf.Clamp((int)gy, 0, down - 1);
                        float fx = gx - cx, fy = gy - cy, halfLine = Mathf.Clamp(gridLineThickness, 0, .4f) * .5f;
                        Color color = colors[(y - yMin) * sampleWidth + x - xMin];
                        color *= 1 - (((cx + cy) & 1) == 0 ? 0 : Mathf.Clamp(checkerVariation, 0, .25f));
                        if (fx < halfLine || fx > 1 - halfLine || fy < halfLine || fy > 1 - halfLine) color = gridColor;
                        pixels[y * width + x] = new Color(color.r, color.g, color.b, 1);
                    }
                }
            }
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "Thumbnail Image", hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels32(pixels); texture.Apply();
            return texture;
        }

        Color32[] SampleFlag(Texture2D source, Color fallback, int across, int down, float tileAspect)
        {
            Texture2D readable = source;
            bool copied = source != null && !source.isReadable;
            if (copied) readable = ReadTexture(source);
            try
            {
                var colors = new Color32[across * down];
                var data = readable != null ? readable.GetPixels32() : null;
                float sourceAspect = readable != null ? (float)readable.width / readable.height : 1;
                for (int y = 0; y < down; y++) for (int x = 0; x < across; x++)
                {
                    float u = (x + .5f) / across, v = (y + .5f) / down;
                    if (flagFit == ThumbnailFlagFit.Contain)
                    {
                        if (sourceAspect > tileAspect) v = (v - .5f) * sourceAspect / tileAspect + .5f;
                        else u = (u - .5f) * tileAspect / sourceAspect + .5f;
                    }
                    else if (flagFit == ThumbnailFlagFit.Cover)
                    {
                        if (sourceAspect > tileAspect) u = (u - .5f) * tileAspect / sourceAspect + .5f;
                        else v = (v - .5f) * sourceAspect / tileAspect + .5f;
                    }
                    Color color = fallback;
                    if (data != null && u >= 0 && u <= 1 && v >= 0 && v <= 1)
                    {
                        float sx = Mathf.Clamp(u * readable.width - .5f, 0, readable.width - 1);
                        float sy = Mathf.Clamp(v * readable.height - .5f, 0, readable.height - 1);
                        int left = (int)sx, bottom = (int)sy;
                        int right = Mathf.Min(left + 1, readable.width - 1), top = Mathf.Min(bottom + 1, readable.height - 1);
                        Color sample = Color.Lerp(
                            Color.Lerp(data[bottom * readable.width + left], data[bottom * readable.width + right], sx - left),
                            Color.Lerp(data[top * readable.width + left], data[top * readable.width + right], sx - left), sy - bottom);
                        color = Color.Lerp(fallback, sample, sample.a);
                    }
                    colors[y * across + x] = color;
                }
                return colors;
            }
            finally { if (copied) Release(readable); }
        }

        static Texture2D ReadTexture(Texture2D source)
        {
            var previous = RenderTexture.active;
            var temporary = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            try
            {
                Graphics.Blit(source, temporary); RenderTexture.active = temporary;
                var readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
                readable.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0); readable.Apply();
                return readable;
            }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(temporary); }
        }

        static bool InsideRoundedBoard(float x, float y, Rect board, float radius)
        {
            float dx = x - Mathf.Clamp(x, board.xMin + radius, board.xMax - radius);
            float dy = y - Mathf.Clamp(y, board.yMin + radius, board.yMax - radius);
            return dx * dx + dy * dy <= radius * radius;
        }

        void ClearPreview()
        {
            var renderer = GetComponent<SpriteRenderer>();
            if (renderer != null && renderer.sprite == previewSprite) renderer.sprite = null;
            Release(previewSprite); Release(previewTexture);
            previewSprite = null; previewTexture = null;
        }
        static void Release(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }
    }
}
