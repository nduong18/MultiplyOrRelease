using System;
using System.IO;
using MultiplyOrRelease;
using UnityEditor;
using UnityEngine;

public static class MarbleFlagPresetGenerator
{
    public const string CatalogPath = "Assets/MultiplyOrRelease/Art/TeamFlags/MarbleFlagCatalog.json";
    public const string PresetFolder = "Assets/MultiplyOrRelease/TeamPresets";
    [Serializable] public sealed class Catalog { public int schemaVersion; public FlagEntry[] flags; }
    [Serializable] public sealed class FlagEntry
    {
        public string slug, displayName, spriteAsset, textureAsset, source;
        public Color territoryColor;
    }

    public static Catalog ReadCatalog()
    {
        if (!File.Exists(CatalogPath)) throw new InvalidOperationException("Run node Tools/FlagAssets/generate-marble-flags.cjs first, then refresh Unity.");
        var catalog = JsonUtility.FromJson<Catalog>(File.ReadAllText(CatalogPath));
        if (catalog == null || catalog.schemaVersion != 1 || catalog.flags == null)
            throw new InvalidOperationException("Invalid MarbleFlag catalog.");
        return catalog;
    }
    static void RequireEditMode()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before generating team assets.");
    }
    static void PrepareTexture(FlagEntry entry)
    {
        var importer = AssetImporter.GetAtPath(entry.textureAsset) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("Missing grid flag texture: " + entry.textureAsset);
        if (importer.isReadable && !importer.mipmapEnabled && importer.textureCompression == TextureImporterCompression.Uncompressed) return;
        importer.isReadable = true; importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.wrapMode = TextureWrapMode.Clamp; importer.filterMode = FilterMode.Bilinear;
        importer.SaveAndReimport();
    }
    public static void FillIdentity(TeamPreset preset, FlagEntry entry)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(entry.spriteAsset);
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(entry.textureAsset);
        if (sprite == null || texture == null || !texture.isReadable)
            throw new InvalidOperationException("Flag assets missing or unreadable for " + entry.displayName);
        var team = preset.team ?? new TeamSettings();
        team.name = entry.displayName;
        team.cannonSprite = team.plinkoSprite = team.projectileSprite = sprite;
        team.territoryFlag = texture; team.territoryColor = entry.territoryColor;
        Color.RGBToHSV(entry.territoryColor, out float h, out float s, out float v);
        Color accent = s < .15f ? Color.white : Color.HSVToRGB(h, Mathf.Max(.65f, s), 1);
        if (entry.slug == "brazil") accent = new Color(1, .84f, .05f);
        team.ammoTextColor = team.projectileColor = team.projectileTrailColor = team.plinkoTrailColor = team.barrelColor = accent;
        team.projectileSpriteTint = team.plinkoBallColor = team.cannonTint = Color.white;
        preset.team = team;
    }
    [MenuItem("Tools/Multiply or Release/Generate Missing MarbleFlag Team Presets")]
    public static void GenerateMissing()
    {
        RequireEditMode();
        var catalog = ReadCatalog();
        var config = AssetDatabase.LoadAssetAtPath<SimulationConfig>(SimulationSceneBuilder.ConfigPath);
        if (config == null) throw new InvalidOperationException("Default simulation config is missing.");
        if (!AssetDatabase.IsValidFolder(PresetFolder)) AssetDatabase.CreateFolder("Assets/MultiplyOrRelease", "TeamPresets");
        int created = 0, preserved = 0;
        foreach (var entry in catalog.flags)
        {
            string fileName = entry.displayName + " Team";
            foreach (char c in Path.GetInvalidFileNameChars()) fileName = fileName.Replace(c, '_');
            string path = PresetFolder + "/" + fileName + ".asset";
            if (AssetDatabase.LoadMainAssetAtPath(path) != null) { preserved++; continue; }
            PrepareTexture(entry);
            var preset = ScriptableObject.CreateInstance<TeamPreset>();
            preset.CaptureFrom(config, 0); // Same gameplay defaults; saved facing inward from upper-left.
            FillIdentity(preset, entry);
            AssetDatabase.CreateAsset(preset, path); AssetDatabase.SaveAssetIfDirty(preset); created++;
        }
        Debug.Log("MarbleFlag teams: " + created + " created, " + preserved + " existing presets preserved (" + catalog.flags.Length + " flags).");
    }
    // Explicit repair only; the normal generator never overwrites user presets.
    public static void RepairBrazil()
    {
        RequireEditMode();
        var entry = Array.Find(ReadCatalog().flags, flag => flag.slug == "brazil");
        if (entry == null) throw new InvalidOperationException("Brazil is missing from the catalog.");
        string path = PresetFolder + "/Brazil Team.asset";
        var preset = AssetDatabase.LoadAssetAtPath<TeamPreset>(path);
        if (preset == null) throw new InvalidOperationException("Brazil Team.asset is missing.");
        PrepareTexture(entry); Undo.RecordObject(preset, "Repair Brazil Team Preset");
        FillIdentity(preset, entry);
        EditorUtility.SetDirty(preset); AssetDatabase.SaveAssetIfDirty(preset);
    }
}
