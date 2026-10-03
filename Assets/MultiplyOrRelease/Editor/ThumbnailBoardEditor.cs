using System.IO;
using MultiplyOrRelease;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ThumbnailBoard))]
public sealed class ThumbnailBoardEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var board = (ThumbnailBoard)target;
        EditorGUILayout.HelpBox("Static thumbnail only. Drag Team Presets into Teams; order is left to right, top to bottom. Columns = 0 for automatic layout. No Play mode needed.", MessageType.Info);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Rebuild Preview", GUILayout.Height(28))) { serializedObject.ApplyModifiedProperties(); board.Rebuild(); }
            if (GUILayout.Button("Export PNG...", GUILayout.Height(28)))
            {
                serializedObject.ApplyModifiedProperties();
                string path = EditorUtility.SaveFilePanel("Export YouTube Thumbnail", Application.dataPath + "/MultiplyOrRelease", "Thumbnail", "png");
                if (!string.IsNullOrEmpty(path)) ExportPng(board, path);
            }
        }
        var layout = board.Layout;
        EditorGUILayout.LabelField("Layout", layout.x + " columns × " + layout.y + " rows • " + board.imageWidth + " × " + board.imageHeight + " px");
        DrawDefaultInspector();
    }

    public static void ExportPng(ThumbnailBoard board, string path)
    {
        var image = board.CreateImage();
        try { File.WriteAllBytes(path, image.EncodeToPNG()); }
        finally { UnityEngine.Object.DestroyImmediate(image); }
        if (path.Replace('\\', '/').StartsWith(Application.dataPath.Replace('\\', '/') + "/")) AssetDatabase.Refresh();
        Debug.Log("Thumbnail PNG saved: " + path, board);
    }
}
