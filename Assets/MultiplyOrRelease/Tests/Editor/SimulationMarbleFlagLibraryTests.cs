using System;
using System.IO;
using System.Linq;
using MultiplyOrRelease;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public class SimulationMarbleFlagLibraryTests
{
    const string Folder = "Assets/MultiplyOrRelease/TeamPresets";
    [Serializable] sealed class Catalog { public FlagEntry[] flags; }
    [Serializable] sealed class FlagEntry { public string slug, displayName, spriteAsset, textureAsset, source; }
    Catalog ReadCatalog() => JsonUtility.FromJson<Catalog>(File.ReadAllText(
        "Assets/MultiplyOrRelease/Art/TeamFlags/MarbleFlagCatalog.json"));

    [Test] public void EveryMarbleFlagHasOneReadyToUsePresetAndManifestEntry()
    {
        var catalog = ReadCatalog();
        var sources = Directory.GetFiles("Assets/MarbleFlag", "*_round.png", SearchOption.AllDirectories);
        Assert.AreEqual(sources.Length, catalog.flags.Length);
        Assert.AreEqual(sources.Length, catalog.flags.Select(flag => flag.slug).Distinct().Count());
        foreach (var entry in catalog.flags)
        {
            var preset = AssetDatabase.LoadAssetAtPath<TeamPreset>(Folder + "/" + entry.displayName + " Team.asset");
            Assert.IsNotNull(preset, entry.displayName);
            Assert.AreEqual(entry.displayName, preset.team.name, entry.slug);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(entry.spriteAsset);
            Assert.IsNotNull(sprite, entry.spriteAsset);
            Assert.AreSame(sprite, preset.team.cannonSprite, entry.slug + " cannon");
            Assert.AreSame(sprite, preset.team.plinkoSprite, entry.slug + " plinko");
            Assert.AreSame(sprite, preset.team.projectileSprite, entry.slug + " projectile");
            Assert.AreSame(AssetDatabase.LoadAssetAtPath<Texture2D>(entry.textureAsset), preset.team.territoryFlag, entry.slug + " grid");
            Assert.That(preset.savedSlot, Is.InRange(0, 3));
            float inward = new[] { -45f, -135f, 45f, 135f }[preset.savedSlot];
            Assert.AreEqual(0, Mathf.DeltaAngle(inward, preset.team.aimDegrees), .0001f, entry.slug + " aim");
            Assert.Greater(preset.team.hitPoints, 0, entry.slug + " health");
            Assert.AreEqual(Color.white, preset.team.cannonTint);
            Assert.AreEqual(Color.white, preset.team.projectileSpriteTint);
        }
    }

    [Test] public void GridFlagsAreReadableRectanglesWithOpaqueCorners()
    {
        foreach (var entry in ReadCatalog().flags)
        {
            var flag = AssetDatabase.LoadAssetAtPath<Texture2D>(entry.textureAsset);
            Assert.IsNotNull(flag, entry.slug); Assert.IsTrue(flag.isReadable, entry.slug);
            Assert.Greater(flag.width, flag.height, entry.slug);
            Assert.AreEqual(1, flag.GetPixel(0, 0).a, .001f, entry.slug);
            Assert.AreEqual(1, flag.GetPixel(flag.width - 1, 0).a, .001f, entry.slug);
            Assert.AreEqual(1, flag.GetPixel(0, flag.height - 1).a, .001f, entry.slug);
            Assert.AreEqual(1, flag.GetPixel(flag.width - 1, flag.height - 1).a, .001f, entry.slug);
        }
    }

    [Test] public void BrazilPresetHasBrazilArtworkAndCanBeLoadedIntoAnyMapCorner()
    {
        var brazil = AssetDatabase.LoadAssetAtPath<TeamPreset>(Folder + "/Brazil Team.asset");
        Assert.AreEqual("Brazil", brazil.team.name);
        Assert.AreEqual("Assets/MarbleFlag/brazil_round.png", AssetDatabase.GetAssetPath(brazil.team.cannonSprite));
        Assert.AreEqual("Assets/MultiplyOrRelease/Art/TeamFlags/brazil.png", AssetDatabase.GetAssetPath(brazil.team.territoryFlag));
        Assert.Greater(brazil.team.territoryColor.g, brazil.team.territoryColor.r);
        Assert.Greater(brazil.team.ammoTextColor.r, .9f); Assert.Greater(brazil.team.ammoTextColor.g, .8f);
        var config = Object.Instantiate(AssetDatabase.LoadAssetAtPath<SimulationConfig>(SimulationSceneBuilderPath));
        try
        {
            float[] inward = { -45, -135, 45, 135 };
            for (int slot = 0; slot < 4; slot++)
            {
                brazil.ApplyTo(config, slot);
                Assert.AreEqual("Brazil", config.teams[slot].name);
                Assert.AreEqual(0, Mathf.DeltaAngle(inward[slot], config.teams[slot].aimDegrees), .0001f);
                Assert.AreSame(brazil.team.cannonSprite, config.teams[slot].cannonSprite);
            }
        }
        finally { Object.DestroyImmediate(config); }
    }
    const string SimulationSceneBuilderPath = "Assets/MultiplyOrRelease/Config/DefaultSimulation.asset";
}
