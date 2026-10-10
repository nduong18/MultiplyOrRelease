using System;
using MultiplyOrRelease;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ConquestSceneBuilder
{
    public const string ScenePath = "Assets/MultiplyOrRelease/Scenes/MultiplyOrRelease24.unity";
    public const string ConfigPath = "Assets/MultiplyOrRelease/Config/Conquest24.asset";

    [MenuItem("Tools/Multiply or Release/Create or Open 24 Country Conquest")]
    public static void CreateOrOpen()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            throw new InvalidOperationException("Stop Play mode and wait for compilation before creating/opening Conquest.");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
        {
            if (SceneManager.GetActiveScene().path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(ScenePath);
            }
            Selection.activeGameObject = UnityEngine.Object.FindFirstObjectByType<ConquestController>()?.gameObject; return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var config = AssetDatabase.LoadAssetAtPath<ConquestConfig>(ConfigPath);
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<ConquestConfig>();
            var original = AssetDatabase.LoadAssetAtPath<SimulationConfig>(SimulationSceneBuilder.ConfigPath);
            config.presentation.font = original != null ? original.presentation.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            config.plinko.font = original != null ? original.plinko.font : null;
            config.winnerFont = original != null ? original.celebration.victoryFont : null;
            config.cannonHitClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/MultiplyOrRelease/Audio/explosion.mp3");
            config.boostCollectClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/MultiplyOrRelease/Audio/collect.mp3");
            config.winnerClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/MultiplyOrRelease/Audio/winner.mp3");
            config.presentation.circleSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/MultiplyOrRelease/Art/Circle.png");
            config.presentation.squareSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/MultiplyOrRelease/Art/Square.png");
            config.presentation.spriteMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/MultiplyOrRelease/Art/SpriteMaterial.mat");
            config.presentation.trailMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/MultiplyOrRelease/Art/TrailMaterial.mat");
            string[] names = { "Vietnam", "United States", "France", "Germany", "Japan", "China",
                "United Kingdom", "Brazil", "Argentina", "Portugal", "Spain", "Italy",
                "South Korea", "Thailand", "Indonesia", "Mexico", "Canada", "Australia",
                "India", "Turkey", "Russia", "Netherlands", "Sweden", "South Africa" };
            for (int i = 0; i < 24; i++)
            {
                var preset = AssetDatabase.LoadAssetAtPath<TeamPreset>("Assets/MultiplyOrRelease/TeamPresets/" + names[i] + " Team.asset");
                var team = preset != null ? preset.team.Copy() : new TeamSettings { name = names[i] };
                team.ammoTextColor = Color.white; team.aimDegrees = 75;
                team.sweepMode = SweepMode.Continuous; team.sweepSpeed = 32 + i % 5 * 3;
                team.sweepDegrees = 120; team.sweepPhase = i * .137f; team.clockwise = i % 2 == 1;
                config.countries[i] = team;
            }
            ApplyFlagColors(config);
            config.grid.gridLineThickness = .09f;
            config.grid.showOwnershipBorders = false;
            config.plinko.pegColor = new Color(.2f, .2f, .2f); config.plinko.backgroundColor = Color.black;
            config.presentation.frameColor = new Color(.18f, .18f, .18f);
            config.presentation.showTeamNames = false; config.presentation.labelSize = .13f;
            config.Validate(); AssetDatabase.CreateAsset(config, ConfigPath); AssetDatabase.SaveAssets();
        }
        // All scene operations happen through this explicit Unity menu command, invoked by MCP.
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
        camera.tag = "MainCamera"; camera.orthographic = true; camera.nearClipPlane = .1f; camera.farClipPlane = 100;
        var controller = new GameObject("24 Country Conquest").AddComponent<ConquestController>();
        controller.config = config; controller.simulationCamera = camera; controller.Rebuild();
        EditorSceneManager.SaveScene(scene, ScenePath); Selection.activeGameObject = controller.gameObject;
        Debug.Log("24 Country Conquest created. Press Play. Configure Conquest24.asset and Apply / Restart.");
    }

    static void ApplyFlagColors(ConquestConfig config)
    {
        var catalog = MarbleFlagPresetGenerator.ReadCatalog();
        foreach (var team in config.countries)
        {
            var entry = Array.Find(catalog.flags, f => f.displayName == team.name);
            Color color = entry != null ? entry.territoryColor : team.territoryColor;
            Color.RGBToHSV(color, out float hue, out float saturation, out float brightness);
            color = Color.HSVToRGB(hue, saturation, Mathf.Max(.95f, brightness));
            team.territoryColor = team.barrelColor = team.projectileColor = team.projectileTrailColor = team.plinkoTrailColor = color;
            team.cannonTint = team.plinkoBallColor = team.projectileSpriteTint = Color.white;
        }
        config.grid.colorBrightness = 1; config.grid.checkerVariation = .025f;
    }

    [MenuItem("Tools/Multiply or Release/24 Countries/Match Colors to Flags")]
    public static void MatchColorsToFlags()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before changing the saved Conquest config.");
        var config = AssetDatabase.LoadAssetAtPath<ConquestConfig>(ConfigPath);
        if (config == null) throw new InvalidOperationException("Create the 24 country scene first.");
        Undo.RecordObject(config, "Match 24 Country Colors to Flags"); ApplyFlagColors(config);
        config.presentation.showTeamNames = false;
        EditorUtility.SetDirty(config); AssetDatabase.SaveAssetIfDirty(config);
        var controller = UnityEngine.Object.FindFirstObjectByType<ConquestController>();
        if (controller != null && controller.config == config) controller.Rebuild();
    }
}

[CustomEditor(typeof(ConquestController))]
public sealed class ConquestControllerEditor : Editor
{
    Editor configEditor;
    bool showConfig;
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector(); var controller = (ConquestController)target;
        EditorGUILayout.HelpBox("24 permanent cannon slots. Enemy hits transfer ownership. Each slot keeps its own Plinko ammo bank. All cannons belonging to one country wins.", MessageType.Info);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Apply / Restart")) controller.Rebuild();
            if (GUILayout.Button("Select Config")) Selection.activeObject = controller.config;
        }
        if (Application.isPlaying)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(controller.Paused ? "Resume" : "Pause")) controller.TogglePause();
                if (GUILayout.Button("Step")) controller.Step();
                if (GUILayout.Button("New Seed")) controller.RestartNewSeed();
            }
            if (controller.Model != null) EditorGUILayout.LabelField("Countries / captures / shots", controller.Model.CountryCount + " / " + controller.Model.captureCount + " / " + controller.Model.shots.Count);
        }
        else using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Preview Winner")) controller.PreviewWinnerCard();
            if (GUILayout.Button("Clear Preview")) controller.ClearWinnerPreview();
        }
        showConfig = EditorGUILayout.Foldout(showConfig, "All conquest settings", true);
        if (showConfig && controller.config != null)
        { CreateCachedEditor(controller.config, null, ref configEditor); configEditor.OnInspectorGUI(); }
    }
    void OnDisable() { if (configEditor != null) DestroyImmediate(configEditor); }
}
