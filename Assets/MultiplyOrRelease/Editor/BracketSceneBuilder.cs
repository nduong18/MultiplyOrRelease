using System;
using System.Linq;
using MultiplyOrRelease;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BracketSceneBuilder
{
    public const string ScenePath = "Assets/MultiplyOrRelease/Scenes/Bracket.unity";
    public const string ConfigPath = "Assets/MultiplyOrRelease/Config/DefaultBracket.asset";

    [MenuItem("Tools/Multiply or Release/Create or Open Bracket")]
    public static void CreateOrOpen()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
        if (SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the current scene before opening Bracket.");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
        {
            EditorSceneManager.OpenScene(ScenePath);
            return;
        }
        var simulationConfig = AssetDatabase.LoadAssetAtPath<SimulationConfig>(SimulationSceneBuilder.ConfigPath);
        if (simulationConfig == null) throw new InvalidOperationException("Create the simulation config first.");
        var catalog = AssetDatabase.FindAssets("t:TeamPreset", new[] { "Assets/MultiplyOrRelease/TeamPresets" })
            .Select(g => AssetDatabase.LoadAssetAtPath<TeamPreset>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(t => t != null && t.team != null).OrderBy(t => t.team.name, StringComparer.OrdinalIgnoreCase).ToArray();
        var config = AssetDatabase.LoadAssetAtPath<BracketConfig>(ConfigPath);
        if (config == null)
        {
            string[] names = { "Indonesia", "Mexico", "France", "China", "Argentina", "Canada", "United Kingdom", "Vietnam",
                "Brazil", "Philippines", "Australia", "India", "Spain", "United States", "Japan", "Germany" };
            config = ScriptableObject.CreateInstance<BracketConfig>();
            config.simulation = simulationConfig;
            config.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            config.circleSprite = simulationConfig.presentation.circleSprite;
            config.championCupSprite = AssetDatabase.LoadAllAssetsAtPath("Assets/MultiplyOrRelease/Art/cup.png").OfType<Sprite>().FirstOrDefault();
            config.teamCatalog = catalog;
            config.teams = names.Select(n => catalog.FirstOrDefault(t => t.team.name == n)).ToArray();
            if (config.teams.Any(t => t == null)) { UnityEngine.Object.DestroyImmediate(config); throw new InvalidOperationException("A default bracket team preset is missing."); }
            AssetDatabase.CreateAsset(config, ConfigPath); AssetDatabase.SaveAssetIfDirty(config);
        }
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var cameraObject = new GameObject("Bracket Camera", typeof(Camera), typeof(AudioListener));
        cameraObject.tag = "MainCamera";
        var camera = cameraObject.GetComponent<Camera>();
        camera.orthographic = true; camera.orthographicSize = 10;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = config.background;
        camera.transform.position = new Vector3(0, 0, -10); camera.nearClipPlane = .1f; camera.farClipPlane = 100;
        var light = new GameObject("Directional Light", typeof(Light)).GetComponent<Light>();
        light.type = LightType.Directional; light.intensity = 1;
        var matchObject = new GameObject("Tournament Match"); matchObject.SetActive(false);
        var simulation = matchObject.AddComponent<SimulationController>();
        simulation.config = simulationConfig; simulation.simulationCamera = camera;
        var bracket = new GameObject("Tournament Bracket").AddComponent<BracketController>();
        bracket.config = config; bracket.bracketCamera = camera; bracket.simulation = simulation;
        bracket.Rebuild();
        EditorSceneManager.SaveScene(scene, ScenePath);
        var scenes = EditorBuildSettings.scenes.ToList();
        if (!scenes.Any(s => s.path == ScenePath)) scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        Selection.activeGameObject = bracket.gameObject;
    }
}
