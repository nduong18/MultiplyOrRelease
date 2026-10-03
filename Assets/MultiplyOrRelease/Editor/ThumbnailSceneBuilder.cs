using System;
using System.Linq;
using MultiplyOrRelease;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ThumbnailSceneBuilder
{
    public const string ScenePath = "Assets/MultiplyOrRelease/Scenes/Thumbnail.unity";

    [MenuItem("Tools/Multiply or Release/Create or Open Thumbnail")]
    public static void CreateOrOpen()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
        if (SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the current scene before opening Thumbnail.");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
        {
            EditorSceneManager.OpenScene(ScenePath);
            var existing = UnityEngine.Object.FindFirstObjectByType<ThumbnailBoard>();
            if (existing != null) { existing.Rebuild(); Selection.activeGameObject = existing.gameObject; }
            return;
        }
        string[] names = { "Vietnam", "United States", "Japan", "Brazil", "France", "United Kingdom",
            "Indonesia", "Mexico", "China", "Argentina", "Canada", "Germany",
            "South Korea", "Australia", "India", "Spain", "Philippines", "Portugal" };
        var presets = names.Select(n => AssetDatabase.LoadAssetAtPath<TeamPreset>(
            "Assets/MultiplyOrRelease/TeamPresets/" + n + " Team.asset")).ToArray();
        if (presets.Any(p => p == null)) throw new InvalidOperationException("A default thumbnail team preset is missing.");
        var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/MultiplyOrRelease/Art/SpriteMaterial.mat");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camera = new GameObject("Thumbnail Camera", typeof(Camera)).GetComponent<Camera>();
        camera.tag = "MainCamera"; camera.nearClipPlane = .1f; camera.farClipPlane = 100;
        var light = new GameObject("Directional Light", typeof(Light)).GetComponent<Light>();
        light.type = LightType.Directional; light.intensity = 1;
        var board = new GameObject("Thumbnail Board").AddComponent<ThumbnailBoard>();
        board.teams = presets; board.thumbnailCamera = camera; board.spriteMaterial = material;
        board.Rebuild();
        EditorSceneManager.SaveScene(scene, ScenePath);
        Selection.activeGameObject = board.gameObject;
        if (SceneView.lastActiveSceneView != null)
        {
            SceneView.lastActiveSceneView.in2DMode = true;
            SceneView.lastActiveSceneView.LookAt(Vector3.zero, Quaternion.identity, 9, true);
        }
    }
}
