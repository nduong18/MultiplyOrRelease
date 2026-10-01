using System;
using System.IO;
using System.Linq;
using MultiplyOrRelease;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SimulationSceneBuilder
{
    public const string Root = "Assets/MultiplyOrRelease";
    public const string ScenePath = Root + "/Scenes/MultiplyOrRelease.unity";
    public const string ConfigPath = Root + "/Config/DefaultSimulation.asset";

    [MenuItem("Tools/Multiply or Release/Create or Open Simulation")]
    public static void CreateOrOpen()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before creating/opening the simulation scene.");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
        {
            if (SceneManager.GetActiveScene().path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(ScenePath);
            }
            var existing = UnityEngine.Object.FindFirstObjectByType<SimulationController>();
            if (existing != null) { existing.Rebuild(); Selection.activeGameObject = existing.gameObject; }
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Directory.CreateDirectory(Root + "/Art"); Directory.CreateDirectory(Root + "/Config");
        Directory.CreateDirectory(Root + "/Scenes"); Directory.CreateDirectory(Root + "/Prefabs");
        AssetDatabase.Refresh();
        var config = AssetDatabase.LoadAssetAtPath<SimulationConfig>(ConfigPath);
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<SimulationConfig>();
            config.presentation.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            config.presentation.circleSprite = GeneratePrimitive("Circle", true);
            config.presentation.squareSprite = GeneratePrimitive("Square", false);
            config.presentation.spriteMaterial = MaterialAsset("SpriteMaterial");
            config.presentation.trailMaterial = MaterialAsset("TrailMaterial");
            string[] names = { "Indonesia", "Mexico", "France", "China" };
            string[] files = { "indonesia", "mexico", "france", "china" };
            Color[] territory = { new Color(.62f, .64f, .66f), new Color(0, .69f, .43f), new Color(.08f, .19f, .85f), new Color(.78f, .08f, .025f) };
            Color[] projectiles = { Color.white, new Color(0, 1, .74f), new Color(.02f, .78f, 1), new Color(1, .84f, .02f) };
            float[] aim = { -45, -135, 45, 135 };
            for (int t = 0; t < 4; t++)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/MarbleFlag/" + files[t] + "_round.png");
                config.teams[t] = new TeamSettings
                {
                    name = names[t], cannonSprite = sprite, plinkoSprite = sprite,
                    territoryFlag = GenerateFlag(t), territoryColor = territory[t],
                    ammoTextColor = projectiles[t], projectileColor = projectiles[t],
                    projectileTrailColor = projectiles[t], plinkoTrailColor = projectiles[t],
                    barrelColor = projectiles[t], plinkoBallColor = Color.white,
                    aimDegrees = aim[t], clockwise = t % 2 == 1, sweepPhase = t * .22f
                };
            }
            config.Validate(); AssetDatabase.CreateAsset(config, ConfigPath); AssetDatabase.SaveAssets();
        }
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cameraObject.tag = "MainCamera";
        var camera = cameraObject.GetComponent<Camera>(); camera.orthographic = true; camera.clearFlags = CameraClearFlags.SolidColor;
        camera.nearClipPlane = .1f; camera.farClipPlane = 100;
        var light = new GameObject("Directional Light", typeof(Light)).GetComponent<Light>();
        light.type = LightType.Directional; light.intensity = 1; light.transform.rotation = Quaternion.Euler(50, -30, 0);
        var simulation = new GameObject("Multiply or Release").AddComponent<SimulationController>();
        simulation.config = config; simulation.simulationCamera = camera;
        PrefabUtility.SaveAsPrefabAsset(simulation.gameObject, Root + "/Prefabs/Simulation.prefab");
        simulation.Rebuild();
        EditorSceneManager.SaveScene(scene, ScenePath);
        var scenes = EditorBuildSettings.scenes.ToList();
        if (!scenes.Any(s => s.path == ScenePath)) scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        Selection.activeGameObject = simulation.gameObject;
        if (SceneView.lastActiveSceneView != null)
        {
            SceneView.lastActiveSceneView.in2DMode = true;
            SceneView.lastActiveSceneView.LookAt(Vector3.zero, Quaternion.identity, 12, true);
        }
        Debug.Log("Multiply or Release scene created. Press Play. Edit DefaultSimulation.asset for all simulation/appearance settings.");
    }
    static Material MaterialAsset(string name)
    {
        string path = Root + "/Art/" + name + ".mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path); if (existing != null) return existing;
        var shader = Shader.Find("Sprites/Default");
        if (shader == null) throw new InvalidOperationException("Sprites/Default shader is unavailable.");
        var mat = new Material(shader) { name = name }; AssetDatabase.CreateAsset(mat, path); return mat;
    }
    static Sprite GeneratePrimitive(string name, bool circle)
    {
        string path = Root + "/Art/" + name + ".png";
        var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path); if (existing != null) return existing;
        const int size = 64; var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
        {
            float radius = new Vector2(x - (size - 1) * .5f, y - (size - 1) * .5f).magnitude;
            pixels[y * size + x] = new Color(1, 1, 1, circle ? Mathf.Clamp01(size * .5f - radius) : 1);
        }
        texture.SetPixels(pixels); texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path); importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single; importer.spritePixelsPerUnit = size;
        importer.mipmapEnabled = false; importer.alphaIsTransparency = true; importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport(); return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
    static Texture2D GenerateFlag(int team)
    {
        string[] names = { "Indonesia", "Mexico", "France", "China" };
        string path = Root + "/Art/Flag" + names[team] + ".png";
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path); if (existing != null) return existing;
        const int w = 384, h = 256; var tex = new Texture2D(w, h, TextureFormat.RGBA32, false); var pixels = new Color[w * h];
        Color red = new Color(.9f, .04f, .07f), green = new Color(0, .42f, .27f), blue = new Color(.02f, .14f, .56f);
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
        {
            float u = (x + .5f) / w, v = (y + .5f) / h; Color color;
            if (team == 0) color = v >= .5f ? red : Color.white;
            else if (team == 1) color = u < 1f / 3 ? green : u > 2f / 3 ? red : Color.white;
            else if (team == 2) color = u < 1f / 3 ? blue : u > 2f / 3 ? red : Color.white;
            else color = red;
            if (team == 1)
            {
                // Placeholder emblem; use a supplied flag texture for exact artwork.
                float ellipse = Mathf.Pow((u - .5f) / .07f, 2) + Mathf.Pow((v - .49f) / .14f, 2);
                if (ellipse > .68f && ellipse < 1 && v < .5f) color = green;
                if (Mathf.Abs(u - .5f) < .025f && v > .42f && v < .59f) color = new Color(.42f, .27f, .08f);
            }
            if (team == 3)
            {
                if (InStar(u, v, .17f, .76f, .095f, 0) || InStar(u, v, .32f, .89f, .029f, 35) ||
                    InStar(u, v, .37f, .8f, .029f, 5) || InStar(u, v, .37f, .66f, .029f, 15) || InStar(u, v, .32f, .56f, .029f, 45)) color = new Color(1, .87f, .06f);
            }
            pixels[y * w + x] = color;
        }
        tex.SetPixels(pixels); tex.Apply(); File.WriteAllBytes(path, tex.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path); var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.isReadable = true; importer.mipmapEnabled = false; importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
    static bool InStar(float u, float v, float cx, float cy, float radius, float rotation)
    {
        var pt = new Vector2((u - cx) * 1.5f, v - cy);
        bool inside = false; Vector2 previous = Vector2.zero;
        for (int i = 0; i <= 10; i++)
        {
            int k = i % 10; float a = (90 + rotation + k * 36) * Mathf.Deg2Rad;
            float r = k % 2 == 0 ? radius : radius * .4f; Vector2 vertex = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
            if (i > 0 && ((vertex.y > pt.y) != (previous.y > pt.y)) && pt.x < (previous.x - vertex.x) * (pt.y - vertex.y) / (previous.y - vertex.y) + vertex.x) inside = !inside;
            previous = vertex;
        }
        return inside;
    }
}

[CustomEditor(typeof(SimulationController))]
public sealed class SimulationControllerEditor : Editor
{
    Editor configEditor;
    bool showConfig = true;
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var simulation = (SimulationController)target;
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("Config asset edits persist. Use Apply / Restart to rebuild with them. Playback controls affect the current run. Restart repeats the same seed; New seed uses the next seed.", MessageType.Info);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Apply / Restart")) simulation.Rebuild();
            if (GUILayout.Button("Select Config")) Selection.activeObject = simulation.config;
        }
        if (Application.isPlaying)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Playback (current run)", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                string action = simulation.Model != null && simulation.Model.phase == MatchPhase.Ready ? "Start" : simulation.Paused ? "Resume" : "Pause";
                if (GUILayout.Button(action)) simulation.TogglePause();
                if (GUILayout.Button("Step")) simulation.Step();
                if (GUILayout.Button("New Seed")) simulation.RestartNewSeed();
            }
            float speed = EditorGUILayout.Slider("Speed", simulation.Speed, .1f, 8);
            if (!Mathf.Approximately(speed, simulation.Speed)) simulation.SetSpeed(speed);
            var grid = (TerritoryStyle)EditorGUILayout.EnumPopup("Grid appearance", simulation.GridStyle);
            if (grid != simulation.GridStyle) simulation.SetGridStyle(grid);
            bool trails = EditorGUILayout.Toggle("Show trails", simulation.TrailsVisible);
            if (trails != simulation.TrailsVisible) simulation.SetTrailsVisible(trails);
            if (simulation.Model != null && simulation.Model.phase == MatchPhase.Finished)
            {
                int winner = simulation.Model.winner;
                EditorGUILayout.HelpBox(winner < 0 ? "DRAW — " + simulation.Model.resultReason : simulation.config.teams[winner].name + " WINS — " + simulation.Model.resultReason, MessageType.Info);
            }
            if (!string.IsNullOrEmpty(simulation.PerformanceMessage)) EditorGUILayout.HelpBox(simulation.PerformanceMessage, MessageType.Warning);
            EditorGUILayout.HelpBox(simulation.Diagnostics(), MessageType.None);
        }
        showConfig = EditorGUILayout.Foldout(showConfig, "All simulation settings", true);
        if (showConfig && simulation.config != null)
        {
            CreateCachedEditor(simulation.config, null, ref configEditor);
            EditorGUI.BeginChangeCheck();
            configEditor.OnInspectorGUI();
            if (EditorGUI.EndChangeCheck() && !Application.isPlaying) simulation.Rebuild();
        }
    }
    void OnDisable() { if (configEditor != null) DestroyImmediate(configEditor); }
    public override bool RequiresConstantRepaint() { return Application.isPlaying; }
}
