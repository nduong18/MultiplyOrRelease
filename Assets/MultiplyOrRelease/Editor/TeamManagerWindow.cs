using System;
using System.Collections.Generic;
using System.IO;
using MultiplyOrRelease;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

public sealed class TeamManagerWindow : EditorWindow
{
    const string PresetFolder = "Assets/MultiplyOrRelease/TeamPresets";
    [SerializeField] SimulationConfig config;
    [SerializeField] TeamPreset teamPreset;
    [SerializeField] TeamLineupPreset lineupPreset;
    [SerializeField] bool alignTerritories = true;
    [SerializeField] bool showDetails;
    [SerializeField] int selectedSlot;
    [SerializeField] Vector2 scroll;
    readonly List<int> slotOrder = new List<int> { 0, 1, 2, 3 };
    ReorderableList slots;
    SerializedObject configObject;

    [MenuItem("Tools/Multiply or Release/Team Manager")]
    public static void Open()
    {
        var controller = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponent<SimulationController>() : null;
        var selectedConfig = Selection.activeObject as SimulationConfig;
        if (selectedConfig == null && controller != null) selectedConfig = controller.config;
        if (selectedConfig == null) selectedConfig = AssetDatabase.LoadAssetAtPath<SimulationConfig>(SimulationSceneBuilder.ConfigPath);
        OpenFor(selectedConfig);
    }
    public static void OpenFor(SimulationConfig config)
    {
        var window = GetWindow<TeamManagerWindow>("Team Manager");
        window.minSize = new Vector2(420, 280);
        window.SetConfig(config); window.Show();
    }
    void OnEnable()
    {
        minSize = new Vector2(420, 280);
        Undo.undoRedoPerformed += OnUndoRedo;
        if (lineupPreset == null) lineupPreset = AssetDatabase.LoadAssetAtPath<TeamLineupPreset>(PresetFolder + "/Original Lineup.asset");
        CreateList();
    }
    void OnDisable() { Undo.undoRedoPerformed -= OnUndoRedo; }
    void OnInspectorUpdate() { Repaint(); } // Asset previews finish asynchronously.
    void OnUndoRedo() { configObject?.Update(); RefreshScene(false); Repaint(); }
    void SetConfig(SimulationConfig value)
    {
        config = value; configObject = value == null ? null : new SerializedObject(value);
        selectedSlot = Mathf.Clamp(selectedSlot, 0, 3);
        CreateList(); Repaint();
    }
    void CreateList()
    {
        slotOrder.Clear(); for (int i = 0; i < 4; i++) slotOrder.Add(i);
        slots = new ReorderableList(slotOrder, typeof(int), true, true, false, false);
        slots.elementHeight = 48;
        slots.drawHeaderCallback = r => EditorGUI.LabelField(r, "Map positions — drag rows to move teams");
        slots.drawElementCallback = DrawSlot;
        slots.onSelectCallback = list => { selectedSlot = Mathf.Clamp(list.index, 0, 3); Repaint(); };
        slots.onReorderCallbackWithDetails = (list, from, to) => MoveTeam(from, to);
        slots.index = selectedSlot;
    }
    void DrawSlot(Rect r, int index, bool active, bool focused)
    {
        if (config == null || config.teams == null || config.teams.Length != 4) return;
        int teamIndex = slotOrder[index];
        var team = config.teams[teamIndex];
        r.y += 3;
        var picture = new Rect(r.x, r.y, 38, 38);
        if (team != null && team.cannonSprite != null)
        {
            var sprite = team.cannonSprite;
            var preview = AssetPreview.GetAssetPreview(sprite) ?? AssetPreview.GetMiniThumbnail(sprite);
            if (preview != null) GUI.DrawTexture(picture, preview, ScaleMode.ScaleToFit, true);
        }
        EditorGUI.LabelField(new Rect(r.x + 46, r.y, r.width - 46, 18), (index + 1) + ". " + TeamSetup.SlotNames[index], EditorStyles.boldLabel);
        EditorGUI.LabelField(new Rect(r.x + 46, r.y + 19, r.width - 46, 18), team != null ? team.name : "Empty team");
    }
    void OnGUI()
    {
        // Context selector remains reachable even with a very short window.
        var next = (SimulationConfig)EditorGUILayout.ObjectField("Simulation Config", config, typeof(SimulationConfig), false);
        if (next != config) SetConfig(next);
        scroll = EditorGUILayout.BeginScrollView(scroll, true, true);
        try
        {
            if (config == null) { EditorGUILayout.HelpBox("Choose a Simulation Config.", MessageType.Info); return; }
            if (config.teams == null || config.teams.Length != 4 || Array.Exists(config.teams, t => t == null))
            {
                EditorGUILayout.HelpBox("The config needs exactly four valid teams. Repair it in the Config Inspector first.", MessageType.Error); return;
            }
            if (configObject == null || configObject.targetObject != config) configObject = new SerializedObject(config);
            EditorGUILayout.HelpBox("Presets save copies, not live links. Drag a row to change that team's map corner, Plinko panel and cannon. Aim rotates with the new corner. Edits persist in the config asset; Ctrl+Z undoes them.", MessageType.Info);
            alignTerritories = EditorGUILayout.Toggle(new GUIContent("Match starting grid to slots", "Move/load team also sets Board Quadrant Owners to 0,1,2,3. Disable to preserve a custom starting-territory mapping."), alignTerritories);
            slots.DoLayoutList();
            selectedSlot = EditorGUILayout.Popup("Selected position", selectedSlot, TeamSetup.SlotNames);
            slots.index = selectedSlot;
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(selectedSlot == 0))
                    if (GUILayout.Button("Move Up")) MoveTeam(selectedSlot, selectedSlot - 1);
                using (new EditorGUI.DisabledScope(selectedSlot == 3))
                    if (GUILayout.Button("Move Down")) MoveTeam(selectedSlot, selectedSlot + 1);
            }
            EditorGUILayout.Space();
            showDetails = EditorGUILayout.Foldout(showDetails, "Selected Team Settings", true);
            if (showDetails)
            {
                configObject.Update();
                var team = configObject.FindProperty("teams").GetArrayElementAtIndex(selectedSlot);
                team.isExpanded = true;
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(team, new GUIContent(TeamSetup.SlotNames[selectedSlot]), true);
                if (EditorGUI.EndChangeCheck())
                {
                    configObject.ApplyModifiedProperties(); EditorUtility.SetDirty(config); RefreshScene(false);
                }
            }
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Reusable team", EditorStyles.boldLabel);
            teamPreset = (TeamPreset)EditorGUILayout.ObjectField("Team Preset", teamPreset, typeof(TeamPreset), false);
            using (new EditorGUI.DisabledScope(teamPreset == null))
                if (GUILayout.Button("Load Team Into Selected Position")) Change("Load Team Preset", () => teamPreset.ApplyTo(config, selectedSlot, alignTerritories));
            if (GUILayout.Button("Save Selected Team As New Preset...")) SaveTeam();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Save / restore all four positions", EditorStyles.boldLabel);
            lineupPreset = (TeamLineupPreset)EditorGUILayout.ObjectField("Lineup Preset", lineupPreset, typeof(TeamLineupPreset), false);
            using (new EditorGUI.DisabledScope(lineupPreset == null))
                if (GUILayout.Button("Load Full Lineup")) Change("Load Team Lineup", () => lineupPreset.ApplyTo(config));
            if (GUILayout.Button("Save Full Lineup As New Preset...")) SaveLineup();
            EditorGUILayout.Space();
            if (Application.isPlaying) EditorGUILayout.HelpBox("The running match uses its own config copy. Apply / Restart to use the new lineup.", MessageType.Info);
            if (GUILayout.Button("Apply / Restart Scene")) RefreshScene(true);
            if (GUILayout.Button("Save Config To Disk")) { EditorUtility.SetDirty(config); AssetDatabase.SaveAssetIfDirty(config); }
        }
        finally { EditorGUILayout.EndScrollView(); }
    }
    public void MoveTeam(int from, int to)
    {
        Change("Move Team Position", () => TeamSetup.Move(config, from, to, alignTerritories));
        selectedSlot = to; CreateList();
    }
    void Change(string undoName, Action operation)
    {
        Undo.RecordObject(config, undoName);
        try { operation(); EditorUtility.SetDirty(config); configObject?.Update(); RefreshScene(false); }
        catch (Exception e) { ShowNotification(new GUIContent(e.Message)); }
        Repaint();
    }
    void RefreshScene(bool allowPlay)
    {
        if (Application.isPlaying && !allowPlay) return;
        foreach (var controller in Resources.FindObjectsOfTypeAll<SimulationController>())
            if (controller.gameObject.scene.IsValid() && controller.config == config) controller.Rebuild();
    }
    static string SavePath(string title, string suggested)
    {
        if (!AssetDatabase.IsValidFolder(PresetFolder)) AssetDatabase.CreateFolder("Assets/MultiplyOrRelease", "TeamPresets");
        foreach (char invalid in Path.GetInvalidFileNameChars()) suggested = suggested.Replace(invalid, '_');
        string path = EditorUtility.SaveFilePanelInProject(title, suggested, "asset", "Choose a new preset asset. Existing assets will not be overwritten.", PresetFolder);
        return string.IsNullOrEmpty(path) ? null : AssetDatabase.GenerateUniqueAssetPath(path);
    }
    void SaveTeam()
    {
        string path = SavePath("Save Team Preset", config.teams[selectedSlot].name + " Team");
        if (path == null) return;
        var preset = CreateInstance<TeamPreset>(); preset.CaptureFrom(config, selectedSlot);
        AssetDatabase.CreateAsset(preset, path); AssetDatabase.SaveAssetIfDirty(preset); teamPreset = preset;
        ShowNotification(new GUIContent("Team preset saved"));
    }
    void SaveLineup()
    {
        string path = SavePath("Save Lineup Preset", "Team Lineup");
        if (path == null) return;
        var preset = CreateInstance<TeamLineupPreset>(); preset.CaptureFrom(config);
        AssetDatabase.CreateAsset(preset, path); AssetDatabase.SaveAssetIfDirty(preset); lineupPreset = preset;
        ShowNotification(new GUIContent("Lineup preset saved"));
    }
}

[CustomEditor(typeof(SimulationConfig))]
public sealed class SimulationConfigEditor : Editor
{
    public override void OnInspectorGUI()
    {
        if (GUILayout.Button("Team Manager — Save / Load / Arrange")) TeamManagerWindow.OpenFor((SimulationConfig)target);
        DrawDefaultInspector();
    }
}
